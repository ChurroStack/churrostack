using ChurrOS.Api.Data;
using ChurrOS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Quartz;
using StackExchange.Redis;
using System.Text.Json;

namespace ChurrOS.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class QuotaResetJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public QuotaResetJob(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var accountId = context.MergedJobDataMap.GetLongValue("accountId");
            var cancellationToken = context.CancellationToken;

            using var scope = _serviceScopeFactory.CreateScope();
            var redis = scope.ServiceProvider.GetService<ConnectionMultiplexer>()!;
            var tenantResovler = scope.ServiceProvider.GetService<ITenantResolver>()!;
            tenantResovler.SetAccountId(accountId);
            tenantResovler.SetIdentity("system");
            var dbContext = scope.ServiceProvider.GetService<ChurrosDbContext>()!;
            var configuration = scope.ServiceProvider.GetService<IConfiguration>()!;
            var account = await dbContext.Set<Domain.Account>()
                .Where(o => o.Id == accountId)
                .Select(o => new { o.Metadata })
                .SingleAsync(cancellationToken);

            long networkQuota = string.IsNullOrWhiteSpace(configuration["Quota:Network"]) ? 25 : long.Parse(configuration["Quota:Network"]!);
            networkQuota = networkQuota * 1024 * 1024 * 1024; // Convert to bytes

            if (account.Metadata?.TryGetValue("quotas", out var jsonQuotas) ?? false)
            {
                if (((JsonElement)jsonQuotas).TryGetProperty("network", out var jsonNetwork))
                {
                    networkQuota = jsonNetwork.GetInt64();
                }
            }

            var quotaService = scope.ServiceProvider.GetService<QuotaService>()!;
            await quotaService.InitializeAsync(networkQuota);
        }
    }
}
