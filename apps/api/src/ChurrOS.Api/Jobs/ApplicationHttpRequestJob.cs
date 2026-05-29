using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.AutoStart;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils;
using DispatchR;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Polly;
using Polly.Retry;
using Quartz;
using System.Data;
using System.Text;
using System.Text.Json;

namespace ChurrOS.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class ApplicationHttpRequestJob : IJob
    {
        // Plumbs per-job context into Polly's OnRetry callback. The static pipeline
        // is shared across all schedule firings, so we cannot capture per-job state
        // in a closure — set it on ResilienceContext.Properties instead.
        private static readonly ResiliencePropertyKey<RetryLogContext> LogContextKey =
            new("ApplicationHttpRequestJob.LogContext");

        // Retry transient/upstream failures during the warm-up window between
        // "Replicas == Available" (which flips app:{id}:running) and the app
        // actually opening its listening socket. Order is Retry inside Timeout,
        // so the 5-min budget caps the total wall-clock across all attempts.
        private static readonly ResiliencePipeline<HttpResponseMessage> SendPipeline =
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(r => (int)r.StatusCode >= 500),
                    MaxRetryAttempts = 10,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    MaxDelay = TimeSpan.FromSeconds(15),
                    OnRetry = args =>
                    {
                        if (args.Context.Properties.TryGetValue(LogContextKey, out var ctx))
                        {
                            var reason = args.Outcome.Exception?.GetType().Name
                                ?? ((int?)args.Outcome.Result?.StatusCode)?.ToString()
                                ?? "unknown";
                            // AttemptNumber is zero-based for the just-failed attempt;
                            // +2 makes this "the total attempt number about to run".
                            ctx.AttemptCount = args.AttemptNumber + 2;
                            ctx.Logger.LogInformation(
                                "[ApplicationHttpRequestJob] retry app={App} schedule={Schedule} attempt={Attempt} delayMs={DelayMs} reason={Reason}",
                                ctx.ApplicationName, ctx.ScheduleName, ctx.AttemptCount,
                                (long)args.RetryDelay.TotalMilliseconds, reason);
                        }
                        return default;
                    }
                })
                .AddTimeout(TimeSpan.FromMinutes(5))
                .Build();

        private sealed class RetryLogContext
        {
            public required ILogger Logger { get; init; }
            public required string ApplicationName { get; init; }
            public required string ScheduleName { get; init; }
            public int AttemptCount { get; set; } = 1;
        }

        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly RunnerService _runnerService;
        private readonly IAppCache _appCache;
        private readonly AutoStartCache _autoStartCache;
        private readonly AutoStartCoordinator _autoStartCoordinator;
        private readonly ILogger<ApplicationHttpRequestJob> _logger;

        public ApplicationHttpRequestJob(IServiceScopeFactory serviceScopeFactory, RunnerService runnerService, IAppCache appCache, AutoStartCache autoStartCache, AutoStartCoordinator autoStartCoordinator, ILogger<ApplicationHttpRequestJob> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _runnerService = runnerService;
            _appCache = appCache;
            _autoStartCache = autoStartCache;
            _autoStartCoordinator = autoStartCoordinator;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var environmentId = context.MergedJobDataMap.GetLongValue("environmentId");
            var applicationId = context.MergedJobDataMap.GetLongValue("applicationId");
            var applicationName = context.MergedJobDataMap.GetString("applicationName")!;
            var portName = context.MergedJobDataMap.GetString("portName")!;
            var name = context.MergedJobDataMap.GetString("name")!;
            var accountId = context.MergedJobDataMap.GetLongValue("accountId");
            var jsonRequest = JsonSerializer.Deserialize<HttpRequestItem>(context.MergedJobDataMap.GetString("httpRequest")!, JsonSettings.Value)!;
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceScopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetService<ITenantResolver>()!;
            var queueService = scope.ServiceProvider.GetService<IQueueService>()!;
            tenantResolver.SetAccountId(accountId);
            tenantResolver.SetIdentity("system");
            var dbContext = scope.ServiceProvider.GetService<ChurrosDbContext>()!;

            DataTable dt = new DataTable();
            dt.Columns.Add("account_id", typeof(long));
            dt.Columns.Add("application_id", typeof(long));
            dt.Columns.Add("environment_id", typeof(long));
            dt.Columns.Add("deployment_name", typeof(string));
            dt.Columns.Add("timestamp", typeof(DateTimeOffset));
            dt.Columns.Add("target", typeof(string));
            dt.Columns.Add("type", typeof(string));
            dt.Columns.Add("reason", typeof(string));
            dt.Columns.Add("message", typeof(string));
            dt.Columns.Add("tags", typeof(string));
            var tags = JsonSerializer.SerializeToElement("{}", JsonSettings.Value);

            var shouldSendHttp = true;
            ShareRouteInfo? route = null;
            var attemptCount = 1;
            try
            {
                // Mirror the /share/* auto-start path so a scheduled job against a stopped app
                // does not silently fail with 502/503 from the runner. We bypass the auto-stop
                // cooldown because the cron is system-initiated — the user explicitly scheduled
                // this request and the cooldown is a flap guard for client-driven loops.
                route = await _autoStartCache.GetRouteAsync(applicationName, dbContext, cancellationToken);
                if (route is not null
                    && route.Mode == ApplicationMode.Application
                    && route.ExecutionStatus != DeploymentExecutionStatus.Running)
                {
                    var outcome = await _autoStartCoordinator.HoldUntilRunningAsync(applicationName, route.AppId, accountId, cancellationToken, bypassCooldown: true);
                    if (outcome != HoldOutcome.Running)
                    {
                        _logger.LogWarning("[ApplicationHttpRequestJob] auto-start did not converge app={App} outcome={Outcome} schedule={Schedule}", applicationName, outcome, name);
                        dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_FAIL", $"The schedule '{name}' could not auto-start the application ({outcome}).", tags);
                        shouldSendHttp = false;
                    }
                    else
                    {
                        await _autoStartCache.InvalidateRouteAsync(applicationName);
                    }
                }

                if (shouldSendHttp)
                {
                    RunnerService.RunnerClient client = await _appCache.GetOrAddAsync($"runner:{accountId}:{environmentId}:metrics", async ctx =>
                    {
                        ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                        var mediator = scope.ServiceProvider.GetService<IMediator>()!;
                        var environment = await dbContext.Set<Domain.Environment>()
                            .AsNoTracking()
                            .Where(o => o.AccountId == accountId && o.Id == environmentId)
                            .Select(o => new { o.Name, o.Host, o.Port, o.EncryptionKey })
                            .SingleAsync();

                        var ecParts = environment.EncryptionKey.Split(':');
                        var encryptionKey = AesGcmEncryption.Decrypt(ecParts[0], dbContext.AccountEncryptionKey, ecParts[1]);
                        return _runnerService.CreateClient(environment.Host[1], environment.Name, environment.Port, encryptionKey, TimeSpan.FromSeconds(30));
                    });

                    var httpMethod = HttpMethod.Parse(jsonRequest.Method);
                    var requestPath = $"/share/{applicationName}/{portName}{(jsonRequest.Path.StartsWith('/') ? "" : "/")}{jsonRequest.Path}";
                    var logContext = new RetryLogContext
                    {
                        Logger = _logger,
                        ApplicationName = applicationName,
                        ScheduleName = name,
                    };

                    var resilienceContext = ResilienceContextPool.Shared.Get(cancellationToken);
                    resilienceContext.Properties.Set(LogContextKey, logContext);
                    HttpResponseMessage result;
                    try
                    {
                        // HttpRequestMessage is single-use; build a fresh one per attempt.
                        // StringContent body comes from an in-memory byte[], so re-building
                        // is cheap.
                        result = await SendPipeline.ExecuteAsync(static async (ctx, state) =>
                        {
                            var attempt = new HttpRequestMessage(state.HttpMethod, state.RequestPath);
                            if (state.JsonRequest.Body != null && state.JsonRequest.Body.Length > 0)
                            {
                                var contentType = state.JsonRequest.Headers?.FirstOrDefault(o => o.Key.Equals("Content-Type", StringComparison.InvariantCultureIgnoreCase)).Value ?? "application/json";
                                attempt.Content = new StringContent(Encoding.UTF8.GetString(state.JsonRequest.Body), Encoding.UTF8, contentType);
                            }
                            return await state.HttpClient.SendAsync(attempt, ctx.CancellationToken);
                        }, resilienceContext, (HttpMethod: httpMethod, RequestPath: requestPath, JsonRequest: jsonRequest, HttpClient: client.HttpClient));
                    }
                    finally
                    {
                        // Sync attempt count here so the outer catch (when the pipeline
                        // exhausts retries via exception) still reports the correct count.
                        attemptCount = logContext.AttemptCount;
                        ResilienceContextPool.Shared.Return(resilienceContext);
                    }

                    if (result.IsSuccessStatusCode)
                    {
                        // A successful scheduled HTTP send counts as user-initiated activity:
                        // bump last_activity so auto-stop doesn't reclaim an app whose only
                        // traffic comes from its own cron. Same Application-mode gate as
                        // auto-start. Gated on success so a permanently-failing schedule
                        // can't keep its app alive by churning 5xx retries.
                        if (route is not null && route.Mode == ApplicationMode.Application)
                        {
                            try
                            {
                                await _autoStartCache.WriteLastActivityAsync(route.AppId, DateTimeOffset.UtcNow);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "[ApplicationHttpRequestJob] last_activity write skipped app={App}", applicationName);
                            }
                        }

                        dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_SUCCESS", $"The schedule '{name}' was run successfully (attempts={attemptCount})", tags);
                    }
                    else
                    {
                        if (attemptCount > 1)
                        {
                            _logger.LogWarning("[ApplicationHttpRequestJob] retries exhausted app={App} schedule={Schedule} attempts={Attempts} lastStatus={Status}", applicationName, name, attemptCount, (int)result.StatusCode);
                        }
                        dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_FAIL", $"The schedule '{name}' fail to run. HTTP {(int)result.StatusCode} (attempts={attemptCount})", tags);
                    }
                }
            }
            catch (Exception ex)
            {
                dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_FAIL", $"The schedule '{name}' fail to run. {ex.Message} (attempts={attemptCount})", tags);
                _logger.LogError(ex, "[ApplicationHttpRequestJob] error running application schedule app={App} schedule={Schedule} attempts={Attempts}", applicationName, name, attemptCount);
            }

            var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
            var isOpen = conn.State == System.Data.ConnectionState.Open;
            if (!isOpen)
                await conn.OpenAsync(cancellationToken);
            try
            {
                ScrapeGenericEventsJob.WriteDataTable(dt, conn);
            }
            finally
            {
                if (!isOpen)
                {
                    await conn.CloseAsync();
                }
            }
        }
    }
}
