
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Collections.Concurrent;
using System.Data;
using System.Net;

namespace ChurrOS.Api.Jobs
{
    public class TracesProcessorJob : BackgroundService
    {
        private record AppInfo(long AccountId, long EnvironmentId, long ApplicationId);
        private readonly IQueueService _queueService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<TracesProcessorJob> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly ICacheService _cacheService;

        public TracesProcessorJob(IQueueService queueService, IServiceScopeFactory serviceScopeFactory, ILogger<TracesProcessorJob> logger, ICacheService cacheService)
        {
            _queueService = queueService;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
            _cacheService = cacheService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("account_id", typeof(long));
            dt.Columns.Add("environment_id", typeof(long));
            dt.Columns.Add("application_id", typeof(long));
            dt.Columns.Add("timestamp", typeof(DateTimeOffset));
            dt.Columns.Add("identity_id", typeof(long));
            dt.Columns.Add("method", typeof(string));
            dt.Columns.Add("protocol", typeof(string));
            dt.Columns.Add("service", typeof(string));
            dt.Columns.Add("host", typeof(string));
            dt.Columns.Add("path", typeof(string));
            dt.Columns.Add("status_code", typeof(int));
            dt.Columns.Add("is_error", typeof(bool));
            dt.Columns.Add("client_ip", typeof(string));
            dt.Columns.Add("request_bytes", typeof(long));
            dt.Columns.Add("response_bytes", typeof(long));
            dt.Columns.Add("duration", typeof(long));
            dt.Columns.Add("tags", typeof(string));

            var observerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var observer = Task.Run(() =>
            {
                while (!stoppingToken.IsCancellationRequested && !observerCancellationTokenSource.IsCancellationRequested)
                {
                    lock (dt)
                    {
                        if (dt.Rows.Count > 0)
                        {
                            try
                            {
                                using var scope = _serviceScopeFactory.CreateScope();
                                var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
                                var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();

                                var isOpen = conn.State == System.Data.ConnectionState.Open;
                                if (!isOpen)
                                {
                                    conn.Open();
                                }
                                try
                                {
                                    WriteDataTable(dt, conn);
                                    dt.Clear();
                                }
                                finally
                                {
                                    if (!isOpen)
                                    {
                                        conn.Close();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error writing application trace.");
                            }
                        }
                    }
                    Task.Delay(1000, stoppingToken).Wait();
                }
            });

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await foreach (var item in _queueService.ConsumeAsync<ApplicationTraceItem>("traces-queue", "traces-group", $"traces-consumer-{Dns.GetHostName()}", stoppingToken))
                    {
                        var semaphore = _locks.GetOrAdd(item.ApplicationName, _ => new SemaphoreSlim(1, 1));
                        await semaphore.WaitAsync();
                        try
                        {
                            using var scope = _serviceScopeFactory.CreateScope();
                            var host = item.Host?.Split(':').First();
                            var key = $"app:{item.ApplicationName}:info:{host}";
                            var currentApp = await _cacheService.GetOrAddAsync(key, async ctx =>
                            {
                                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                                var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();

                                var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
                                var isOpen = conn.State == System.Data.ConnectionState.Open;
                                if (!isOpen)
                                    await conn.OpenAsync();
                                try
                                {
                                    var apps = new DataTable();
                                    apps.Columns.Add("id", typeof(long));
                                    apps.Columns.Add("environment_id", typeof(long));
                                    apps.Columns.Add("account_id", typeof(long));
                                    apps.Columns.Add("domains", typeof(System.Array));

                                    await using var appCmd = new NpgsqlCommand("""
                                    SELECT
                                        a.id,
                                        a.environment_id,
                                        a.account_id,
                                        e.domains
                                    FROM cs.application a
                                    JOIN cs.account e ON a.account_id = e.id
                                    WHERE a.name = @name;
                                    """, conn);

                                    appCmd.Parameters.Add(new NpgsqlParameter("name", item.ApplicationName));

                                    await using (var appReader = await appCmd.ExecuteReaderAsync())
                                    {
                                        apps.Load(appReader);
                                    }

                                    if (apps.Rows.Count == 0)
                                        throw new ArgumentException($"Application '{item.ApplicationName}' not found");

                                    if (apps.Rows.Count > 1)
                                    {
                                        var app = apps.AsEnumerable()
                                            .Where(o => ((string[])o["domains"]).Any(d => d.Equals(host, StringComparison.InvariantCultureIgnoreCase)))
                                            .FirstOrDefault();

                                        if (app is null)
                                            throw new ArgumentException($"Application '{item.ApplicationName}' not found");

                                        return new AppInfo((long)app["account_id"], (long)app["environment_id"], (long)app["id"]);
                                    }
                                    else
                                    {
                                        var app = apps.Rows[0];
                                        return new AppInfo((long)app["account_id"], (long)app["environment_id"], (long)app["id"]);
                                    }
                                }
                                finally
                                {
                                    if (!isOpen)
                                    {
                                        await conn.CloseAsync();
                                    }
                                }
                            }, stoppingToken);

                            var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
                            tenantResolver.SetAccountId(currentApp.AccountId);

                            long? identityId = null;
                            if (!string.IsNullOrWhiteSpace(item.IdentityName))
                            {
                                tenantResolver.SetIdentity(item.IdentityName);
                                var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
                                identityId = dbContext.IdentityId;
                            }

                            var quotaService = scope.ServiceProvider.GetRequiredService<QuotaService>();
                            await quotaService.IncrementUsageAsync(QuotaService.QuotaType.Network, item.RequestBytes + item.ResponseBytes);

                            lock (dt)
                            {
                                dt.Rows.Add(currentApp.AccountId,
                                    currentApp.EnvironmentId,
                                    currentApp.ApplicationId,
                                    item.Timestamp,
                                    identityId,
                                    item.Method,
                                    item.Protocol,
                                    item.Service,
                                    item.Host,
                                    item.Path,
                                    item.StatusCode,
                                    item.IsError,
                                    item.ClientIp,
                                    item.RequestBytes,
                                    item.ResponseBytes,
                                    item.Duration,
                                    item.Tags
                                );

                                var commonLabels = new Dictionary<string, string>
                                {
                                    { "application_id", currentApp.ApplicationId.ToString() },
                                    { "environment_id", currentApp.EnvironmentId.ToString() },
                                    { "is_error", item.IsError.ToString().ToLowerInvariant() }
                                };
                                if (!string.IsNullOrWhiteSpace(item.Service))
                                {
                                    commonLabels.Add("service", item.Service);
                                }
                                if (!string.IsNullOrWhiteSpace(item.ClientIp))
                                {
                                    commonLabels.Add("client_ip", item.ClientIp);
                                }
                                if (!string.IsNullOrWhiteSpace(item.IdentityName))
                                {
                                    commonLabels.Add("identity_name", item.IdentityName);
                                }

                                var metricsAggregatorService = scope.ServiceProvider.GetService<MetricsAggregatorService>()!;

                                var bytesInLabels = commonLabels.ToDictionary(entry => entry.Key, entry => entry.Value);
                                bytesInLabels.Add("metric", "bytes_in");
                                var bytesInMetric = new MetricItem(currentApp.AccountId, bytesInLabels, item.Timestamp, item.RequestBytes, Models.Dtos.MetricType.Counter);
                                metricsAggregatorService.AddMetric(bytesInMetric);

                                var bytesOutLabels = commonLabels.ToDictionary(entry => entry.Key, entry => entry.Value);
                                bytesOutLabels.Add("metric", "bytes_out");
                                var bytesOutMetric = new MetricItem(currentApp.AccountId, bytesOutLabels, item.Timestamp, item.ResponseBytes, Models.Dtos.MetricType.Counter);
                                metricsAggregatorService.AddMetric(bytesOutMetric);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error writing traces.");
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing traces.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
            observerCancellationTokenSource.Cancel();
        }

        private static void WriteDataTable(DataTable dt, NpgsqlConnection conn)
        {
            using (var writer = conn.BeginBinaryImport("COPY cs.application_trace (account_id, environment_id, application_id, \"timestamp\", identity_id, method, protocol, service, host, path, status_code, is_error, client_ip, request_bytes, response_bytes, duration, tags) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (DataRow row in dt.Rows)
                {
                    writer.StartRow();
                    writer.Write(row["account_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["environment_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["application_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["timestamp"], NpgsqlTypes.NpgsqlDbType.TimestampTz);
                    writer.Write(row["identity_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["method"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["protocol"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["service"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["host"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["path"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["status_code"], NpgsqlTypes.NpgsqlDbType.Integer);
                    writer.Write(row["is_error"], NpgsqlTypes.NpgsqlDbType.Boolean);
                    writer.Write(row["client_ip"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["request_bytes"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["response_bytes"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["duration"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["tags"], NpgsqlTypes.NpgsqlDbType.Text);
                }
                writer.Complete();
            }
        }
    }
}
