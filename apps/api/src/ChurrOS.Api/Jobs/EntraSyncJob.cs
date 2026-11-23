using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Entra;
using Quartz;

namespace ChurrOS.Api.Jobs
{
    /// <summary>
    /// One Entra sync run for one tenant. Fired by the per-tenant cron trigger (trigger=schedule) or by a manual
    /// TriggerJob (trigger=manual). [DisallowConcurrentExecution] is enforced cluster-wide per job key.
    /// JobDataMap values are strings (Quartz UseProperties=true).
    /// </summary>
    [DisallowConcurrentExecution]
    public class EntraSyncJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public EntraSyncJob(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var accountId = long.Parse(context.MergedJobDataMap.GetString("accountId")!);
            var trigger = context.MergedJobDataMap.GetString("trigger") ?? "schedule";

            using var scope = _serviceScopeFactory.CreateScope();
            var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
            tenantResolver.SetAccountId(accountId);
            tenantResolver.SetIdentity("system");

            var service = scope.ServiceProvider.GetRequiredService<EntraSyncService>();
            await service.RunAsync(accountId, trigger, context.CancellationToken);
        }
    }
}
