using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.AutoStart;
using DispatchR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Jobs
{
    /// <summary>
    /// Periodically scans every tenant for apps with auto-stop enabled and stops the ones
    /// idle longer than their configured threshold. Triggered every 5 minutes.
    /// </summary>
    [DisallowConcurrentExecution]
    public class AutoStopEvaluatorJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly AutoStartCache _autoStartCache;
        private readonly ILogger<AutoStopEvaluatorJob> _logger;

        public AutoStopEvaluatorJob(IServiceScopeFactory serviceScopeFactory, AutoStartCache autoStartCache, ILogger<AutoStopEvaluatorJob> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _autoStartCache = autoStartCache;
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

            var now = DateTimeOffset.UtcNow;
            var stoppedCount = 0;

            foreach (var accountId in accountIds)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
                    tenantResolver.SetAccountId(accountId);
                    tenantResolver.SetIdentity("system");

                    var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
                    // TODO(scale): push the metadata->'autoStop'->>'enabled' = 'true' filter
                    // into SQL via FromSqlRaw / EF.Functions JSON operators, plus a partial
                    // expression index, once fleets exceed a few hundred apps per tenant.
                    // For V1 this loads every Running app per tenant and filters client-side.
                    // Workspace apps are excluded — see ShareRouteInfo's Workspace gate; V1
                    // auto-stop semantics are app-scoped, which would conflate per-user
                    // workspace deployments.
                    var runningApps = await dbContext.Set<Domain.Application>()
                        .AsNoTracking()
                        .Where(a => a.Mode == ApplicationMode.Application
                                 && a.Deployments!.Any(d => d.ExecutionStatus == DeploymentExecutionStatus.Running))
                        .Select(a => new
                        {
                            a.Id,
                            a.Name,
                            a.Metadata,
                            RunningDeployments = a.Deployments!
                                .Where(d => d.ExecutionStatus == DeploymentExecutionStatus.Running)
                                .Select(d => new { d.Name, d.ModifiedAt })
                                .ToList()
                        })
                        .ToListAsync(cancellationToken);

                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    foreach (var app in runningApps)
                    {
                        var settings = AutoStartSettings.From(app.Metadata);
                        if (!settings.AutoStopEnabled) continue;

                        var lastActivity = await _autoStartCache.GetLastActivityAsync(app.Id);

                        foreach (var dep in app.RunningDeployments)
                        {
                            var reference = lastActivity ?? dep.ModifiedAt;
                            var idleFor = now - reference;
                            if (idleFor < TimeSpan.FromMinutes(settings.IdleMinutes)) continue;

                            // Short-circuit if we've already fired Stop for this app within
                            // the AutoStopInflightTtl window. ExecutionStatus stays "Running"
                            // in the DB until ScrapeDeploymentStateJob observes the runner-
                            // side transition, so without this guard the next 5-min tick
                            // would re-issue Stop and re-set the cooldown for the same app.
                            if (!await _autoStartCache.TryClaimStopAsync(app.Id))
                            {
                                _logger.LogDebug("[AutoStop] skip recent-stop inflight app={App}", app.Name);
                                continue;
                            }

                            try
                            {
                                _logger.LogInformation("[AutoStop] stopping app={App} deployment={Dep} idle={IdleMin}m threshold={ThresholdMin}m tenant={Tenant}",
                                    app.Name, dep.Name, (int)idleFor.TotalMinutes, settings.IdleMinutes, accountId);
                                await mediator.Send(new StopApplication(app.Name, dep.Name)
                                {
                                    BypassAcl = true,
                                    SetCooldown = true
                                }, cancellationToken);
                                stoppedCount++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[AutoStop] stop failed app={App} deployment={Dep} tenant={Tenant}", app.Name, dep.Name, accountId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AutoStop] tenant scan failed account={Account}", accountId);
                }
            }

            if (stoppedCount > 0)
            {
                _logger.LogInformation("[AutoStop] tick complete stopped={Count}", stoppedCount);
            }
        }
    }
}
