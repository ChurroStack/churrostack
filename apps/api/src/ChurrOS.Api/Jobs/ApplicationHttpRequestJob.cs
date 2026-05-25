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
using Quartz;
using System.Data;
using System.Text;
using System.Text.Json;

namespace ChurrOS.Api.Jobs
{
    public class ApplicationHttpRequestJob : IJob
    {
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
                    var message = new HttpRequestMessage(httpMethod, $"/share/{applicationName}/{portName}{(jsonRequest.Path.StartsWith('/') ? "" : "/")}{jsonRequest.Path}");
                    if (jsonRequest.Body != null && jsonRequest.Body.Length > 0)
                    {
                        var contentType = jsonRequest.Headers?.FirstOrDefault(o => o.Key.Equals("Content-Type", StringComparison.InvariantCultureIgnoreCase)).Value ?? "application/json";
                        message.Content = new StringContent(Encoding.UTF8.GetString(jsonRequest.Body), Encoding.UTF8, contentType);
                    }

                    var result = await client.HttpClient.SendAsync(message, cancellationToken);

                    // The scheduled HTTP send counts as user-initiated activity: bump
                    // last_activity so auto-stop doesn't reclaim an app whose only traffic
                    // comes from its own cron. Same Application-mode gate as auto-start.
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

                    if (result.IsSuccessStatusCode)
                    {
                        dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_SUCCESS", $"The schedule '{name}' was run successfully", tags);
                    }
                    else
                    {
                        dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_FAIL", $"The schedule '{name}' fail to run. HTTP {(int)result.StatusCode}", tags);
                    }
                }
            }
            catch (Exception ex)
            {
                dt.Rows.Add(accountId, applicationId, environmentId, applicationName, DateTimeOffset.Now, name, "SCHEDULER", "SCHEDULE_FAIL", $"The schedule '{name}' fail to run. {ex.Message}", tags);
                _logger.LogError(ex, "Error running application schedule.");
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
