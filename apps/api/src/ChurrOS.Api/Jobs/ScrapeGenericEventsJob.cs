using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using DispatchR;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Quartz;
using System.Data;

namespace ChurrOS.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class ScrapeGenericEventsJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly RunnerService _runnerService;
        private readonly ILogger<ScrapeGenericEventsJob> _logger;
        private readonly IAppCache _appCache;
        private readonly ICacheService _cacheService;

        public ScrapeGenericEventsJob(IServiceScopeFactory serviceScopeFactory, RunnerService runnerService, ILogger<ScrapeGenericEventsJob> logger, ICacheService cacheService, IAppCache appCache)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _runnerService = runnerService;
            _logger = logger;
            _cacheService = cacheService;
            _appCache = appCache;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var environmentId = context.MergedJobDataMap.GetLongValue("environmentId");
            var accountId = context.MergedJobDataMap.GetLongValue("accountId");
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceScopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetService<ITenantResolver>()!;
            tenantResolver.SetAccountId(accountId);
            tenantResolver.SetIdentity("system");
            var dbContext = scope.ServiceProvider.GetService<ChurrosDbContext>()!;

            RunnerService.RunnerClient client = await _appCache.GetOrAddAsync($"runner:{accountId}:{environmentId}:events", async ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var mediator = scope.ServiceProvider.GetService<IMediator>()!;
                var environment = await dbContext.Set<Domain.Environment>()
                    .Where(o => o.AccountId == accountId && o.Id == environmentId)
                    .Select(o => new { o.Name, o.Host, o.Port, o.EncryptionKey })
                    .SingleAsync();

                var ecParts = environment.EncryptionKey.Split(':');
                var encryptionKey = AesGcmEncryption.Decrypt(ecParts[0], dbContext.AccountEncryptionKey, ecParts[1]);
                return _runnerService.CreateClient(environment.Host[1], environment.Name, environment.Port, encryptionKey);
            });

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

            var observerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var observer = Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested && !observerCancellationTokenSource.IsCancellationRequested)
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
                    Task.Delay(1000, cancellationToken).Wait();
                }
            });

            var conn = (NpgsqlConnection)dbContext.Database.GetDbConnection();
            var isOpen = conn.State == System.Data.ConnectionState.Open;
            if (!isOpen)
                await conn.OpenAsync(cancellationToken);
            try
            {
                await foreach (var @event in client.MonitorEventsAsync(cancellationToken))
                {
                    if (!@event.Annotations.ContainsKey("churrostack.com/deployment-id") &&
                        !@event.Annotations.ContainsKey("churrostack.com/app-id"))
                        continue;

                    var deploymentName = @event.Annotations["churrostack.com/deployment-id"];
                    var appName = @event.Annotations["churrostack.com/app-id"];

                    var appId = await _cacheService.GetOrAddAsync($"app:{appName}:id", async ctx =>
                    {
                        ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                        var existingAppId = await dbContext.Set<Application>()
                            .AsNoTracking()
                            .Where(o => o.Name == appName && o.EnvironmentId == environmentId)
                            .Select(o => (long?)o.Id)
                            .FirstOrDefaultAsync();
                        return existingAppId;
                    }, cancellationToken);

                    if (appId == null)
                    {
                        continue;
                    }

                    lock (dt)
                    {
                        dt.Rows.Add(accountId, appId, environmentId, deploymentName, @event.Timestamp, @event.Name, @event.Type, @event.Reason, @event.Note, "{}");
                    }
                }
                lock (dt)
                {
                    if (dt.Rows.Count > 0)
                    {
                        WriteDataTable(dt, conn);
                        dt.Clear();
                    }
                }
            }
            finally
            {
                if (!isOpen)
                {
                    await conn.CloseAsync();
                }
            }
            observerCancellationTokenSource.Cancel();
        }

        internal static void WriteDataTable(DataTable dt, NpgsqlConnection conn)
        {
            using (var writer = conn.BeginBinaryImport("COPY cs.application_event (account_id, application_id, environment_id, deployment_name, \"timestamp\", target, type, reason, message, tags) FROM STDIN (FORMAT BINARY)"))
            {
                foreach (DataRow row in dt.Rows)
                {
                    writer.StartRow();
                    writer.Write(row["account_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["application_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["environment_id"], NpgsqlTypes.NpgsqlDbType.Bigint);
                    writer.Write(row["deployment_name"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["timestamp"], NpgsqlTypes.NpgsqlDbType.TimestampTz);
                    writer.Write(row["target"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["type"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["reason"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["message"], NpgsqlTypes.NpgsqlDbType.Text);
                    writer.Write(row["tags"], NpgsqlTypes.NpgsqlDbType.Jsonb);
                }

                writer.Complete();
            }
        }
    }
}
