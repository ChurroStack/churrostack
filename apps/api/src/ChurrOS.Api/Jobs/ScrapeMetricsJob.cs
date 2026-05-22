using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using DispatchR;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Quartz;
using StackExchange.Redis;
using System.Data;

namespace ChurrOS.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class ScrapeMetricsJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly RunnerService _runnerService;
        private readonly IAppCache _appCache;
        private readonly ICacheService _cacheService;
        private readonly MetricsAggregatorService _metricsAggregatorService;
        private readonly ILogger<ScrapeMetricsJob> _logger;

        public ScrapeMetricsJob(IServiceScopeFactory serviceScopeFactory, RunnerService runnerService, IAppCache appCache, ICacheService cacheService, MetricsAggregatorService metricsAggregatorService, ILogger<ScrapeMetricsJob> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _runnerService = runnerService;
            _appCache = appCache;
            _cacheService = cacheService;
            _metricsAggregatorService = metricsAggregatorService;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var environmentId = context.MergedJobDataMap.GetLongValue("environmentId");
            var accountId = context.MergedJobDataMap.GetLongValue("accountId");
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceScopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetService<ITenantResolver>()!;
            var queueService = scope.ServiceProvider.GetService<IQueueService>()!;
            var clientNotificationService = scope.ServiceProvider.GetService<ClientNotificationService>()!;
            tenantResolver.SetAccountId(accountId);
            tenantResolver.SetIdentity("system");
            var dbContext = scope.ServiceProvider.GetService<ChurrosDbContext>()!;

            try
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
                    return _runnerService.CreateClient(environment.Host[1], environment.Name, environment.Port, encryptionKey);
                });

                bool checkStatus = true;
                var metrics = await client.ScrapeMetricsAsync(cancellationToken);
                var usageMetrics = new List<(long AppId, string AppName, string DeploymentName, double? CpuUsage, double? MemoryUsage, double? StorageUsage, double? GpuUsage)>();
                foreach (var metric in metrics)
                {
                    if (checkStatus)
                    {
                        var environment = await dbContext.Set<Domain.Environment>()
                            .Where(o => o.AccountId == accountId && o.Id == environmentId).FirstOrDefaultAsync();

                        if (environment is not null && (environment.Health is null || environment.Health.Healthy == false))
                        {
                            environment.Health = new Models.Dtos.Environment.EnvironmentHealthItem(true, DateTimeOffset.Now, null);
                            dbContext.Update(environment);
                            await dbContext.SaveChangesAsync(cancellationToken);
                            await clientNotificationService.NotifyChangeAsync(environment.AccountId, environment.Name, ClientNotificationService.NotificationTargetType.Environment, cancellationToken);
                        }
                        checkStatus = false;
                    }

                    var appName = metric.AppName;

                    var appId = await _cacheService.GetOrAddAsync($"app:{appName}:id", async (ctx) =>
                    {
                        var app = await dbContext.Set<Domain.Application>().Where(o => o.AccountId == accountId && o.Name == appName).FirstOrDefaultAsync();
                        return app?.Id;
                    }, cancellationToken);

                    if (appId is null || appId == 0)
                    {
                        continue;
                    }

                    var now = DateTimeOffset.Now;
                    var labels = new Dictionary<string, string>()
                    {
                        { "application_id", appId.ToString()! },
                        { "environment_id", environmentId.ToString() },
                        { "deployment_name", metric.Name },
                        { "target", metric.Target}
                    };
                    if (metric.CpuUsage.HasValue)
                    {
                        // CPU usage from the runner is an instantaneous reading (cores), like memory — a gauge, not a counter.
                        _metricsAggregatorService.AddMetric(new MetricItem(accountId, AddMetricName(labels, "cpu_usage"), DateTimeOffset.Now, metric.CpuUsage.Value, MetricType.Gauge));
                    }
                    if (metric.GpuUsage.HasValue)
                    {
                        // GPU usage from the runner is an instantaneous reading — a gauge, not a counter.
                        _metricsAggregatorService.AddMetric(new MetricItem(accountId, AddMetricName(labels, "gpu_usage"), DateTimeOffset.Now, metric.GpuUsage.Value, MetricType.Gauge));
                    }
                    if (metric.MemoryUsage.HasValue)
                    {
                        _metricsAggregatorService.AddMetric(new MetricItem(accountId, AddMetricName(labels, "memory_usage"), DateTimeOffset.Now, metric.MemoryUsage.Value, MetricType.Gauge));
                    }
                    if (metric.StorageUsage.HasValue)
                    {
                        _metricsAggregatorService.AddMetric(new MetricItem(accountId, AddMetricName(labels, "storage_usage"), DateTimeOffset.Now, metric.StorageUsage.Value, MetricType.Gauge));
                    }

                    usageMetrics.Add((appId.Value, appName, metric.Name, metric.CpuUsage ?? 0, metric.MemoryUsage ?? 0, metric.StorageUsage ?? 0, metric.GpuUsage ?? 0));
                }

                var connectionMultiplexer = scope.ServiceProvider.GetService<IConnectionMultiplexer>()!;
                var db = connectionMultiplexer.GetDatabase();
                foreach (var appUsage in usageMetrics.GroupBy(o => o.AppId))
                {
                    var appName = appUsage.FirstOrDefault().AppName;
                    if (string.IsNullOrWhiteSpace(appName))
                        continue;

                    var usageKey = $"churros_tenant:{accountId}:app:{appUsage.Key}:resource_usage";
                    await db.HashSetAsync(usageKey,
                    [
                        new HashEntry("cpu", appUsage.Sum(o=>o.CpuUsage) ?? 0),
                        new HashEntry("gpu", appUsage.Sum(o=>o.GpuUsage) ?? 0),
                        new HashEntry("memory", appUsage.Sum(o=>o.MemoryUsage) ?? 0),
                        new HashEntry("storage", appUsage.Sum(o=>o.StorageUsage) ?? 0),
                    ]);
                    db.KeyExpire(usageKey, TimeSpan.FromHours(1));

                    foreach (var usage in appUsage)
                    {
                        if (appName == usage.DeploymentName)
                            continue;

                        usageKey = $"churros_tenant:{accountId}:app:{appUsage.Key}:resource_usage:{usage.DeploymentName}";
                        await db.HashSetAsync(usageKey,
                        [
                            new HashEntry("cpu", usage.CpuUsage ?? 0),
                            new HashEntry("gpu", usage.GpuUsage ?? 0),
                            new HashEntry("memory", usage.MemoryUsage ?? 0),
                            new HashEntry("storage", usage.StorageUsage ?? 0),
                        ]);
                        db.KeyExpire(usageKey, TimeSpan.FromHours(1));
                    }
                }
            }
            catch (Exception ex)
            {
                var environment = await dbContext.Set<Domain.Environment>()
                    .Where(o => o.AccountId == accountId && o.Id == environmentId).FirstOrDefaultAsync();
                if (environment is not null && (environment.Health is null || environment.Health.Healthy))
                {
                    environment.Health = new Models.Dtos.Environment.EnvironmentHealthItem(false, DateTimeOffset.Now, ex.Message);
                    dbContext.Update(environment);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await clientNotificationService.NotifyChangeAsync(environment.AccountId, environment.Name, ClientNotificationService.NotificationTargetType.Environment, cancellationToken);
                }
                _logger.LogError(ex, "Error writting aggregated metrics to database.");
            }

            try
            {
                var scrappedMetrics = _metricsAggregatorService.ScrapeMetrics();
                if (scrappedMetrics.Any())
                {
                    var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
                    var isOpen = conn.State == System.Data.ConnectionState.Open;
                    if (!isOpen)
                    {
                        conn.Open();
                    }
                    try
                    {
                        using (var writer = conn.BeginBinaryImport("COPY cs.metric_value (account_id, metric_id, \"timestamp\", value) FROM STDIN (FORMAT BINARY)"))
                        {
                            foreach (var metric in scrappedMetrics)
                            {
                                writer.StartRow();
                                writer.Write(metric.Metric.AccountId, NpgsqlTypes.NpgsqlDbType.Bigint);
                                writer.Write(metric.MetricId, NpgsqlTypes.NpgsqlDbType.Bigint);
                                writer.Write(metric.Metric.Timestamp, NpgsqlTypes.NpgsqlDbType.TimestampTz);
                                writer.Write(metric.Metric.Value, NpgsqlTypes.NpgsqlDbType.Double);
                            }

                            writer.Complete();
                        }
                    }
                    finally
                    {
                        if (!isOpen)
                        {
                            conn.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writting aggregated metrics to database.");
            }
        }

        private IDictionary<string, string> AddMetricName(Dictionary<string, string> tags, string metricName)
        {
            var newDict = new Dictionary<string, string>(tags)
            {
                { "metric", metricName }
            };
            if (tags is not null)
            {
                foreach (var kvp in tags)
                {
                    newDict.TryAdd(kvp.Key, kvp.Value);
                }
            }
            return newDict;
        }
    }
}
