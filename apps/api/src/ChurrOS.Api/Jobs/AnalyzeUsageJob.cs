using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Data;
using ChurrOS.Api.Services;
using DispatchR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Jobs
{
    /// <summary>
    /// Nightly job that recomputes Application Size recommendations for every
    /// application of every tenant.
    /// </summary>
    [DisallowConcurrentExecution]
    public class AnalyzeUsageJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<AnalyzeUsageJob> _logger;

        public AnalyzeUsageJob(IServiceScopeFactory serviceScopeFactory, ILogger<AnalyzeUsageJob> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var cancellationToken = context.CancellationToken;

            long[] accountIds;
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
                accountIds = await dbContext.Set<Domain.Account>()
                    .AsNoTracking()
                    .Select(a => a.Id)
                    .ToArrayAsync(cancellationToken);
            }

            _logger.LogInformation("AnalyzeUsageJob: starting, tenants={Tenants}", accountIds.Length);

            var succeeded = 0;
            var failed = 0;

            foreach (var accountId in accountIds)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
                    tenantResolver.SetAccountId(accountId);
                    tenantResolver.SetIdentity("system");

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.Send(new AnalyzeApplicationUsage(skipAuthorization: true), cancellationToken);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "AnalyzeUsageJob: failed for account {AccountId}.", accountId);
                }
            }

            _logger.LogInformation(
                "AnalyzeUsageJob: completed, tenants={Tenants} succeeded={Succeeded} failed={Failed}",
                accountIds.Length, succeeded, failed);
        }
    }
}
