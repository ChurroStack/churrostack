using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using DispatchR;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Quartz;
using System.Data;

namespace ChurrOS.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class ScrapeDeploymentStateJob : IJob
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly RunnerService _runnerService;
        private readonly IAppCache _appCache;
        private readonly ILogger<ScrapeDeploymentStateJob> _logger;
        private readonly ClientNotificationService _clientNotificationService;
        private readonly ICacheService _cacheService;

        public ScrapeDeploymentStateJob(IServiceScopeFactory serviceScopeFactory, RunnerService runnerService, IAppCache appCache, ILogger<ScrapeDeploymentStateJob> logger, ClientNotificationService clientNotificationService, ICacheService cacheService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _runnerService = runnerService;
            _appCache = appCache;
            _logger = logger;
            _clientNotificationService = clientNotificationService;
            _cacheService = cacheService;
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

            var mediator = scope.ServiceProvider.GetService<IMediator>()!;
            RunnerService.RunnerClient client = await _appCache.GetOrAddAsync($"runner:{accountId}:{environmentId}:deployment_state", async ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var environment = await dbContext.Set<Domain.Environment>()
                    .Where(o => o.AccountId == accountId && o.Id == environmentId)
                    .Select(o => new { o.Name, o.Host, o.Port, o.EncryptionKey })
                    .SingleAsync();

                var ecParts = environment.EncryptionKey.Split(':');
                var encryptionKey = AesGcmEncryption.Decrypt(ecParts[0], dbContext.AccountEncryptionKey, ecParts[1]);
                return _runnerService.CreateClient(environment.Host[1], environment.Name, environment.Port, encryptionKey);
            });

            await foreach (var deploymentState in client.MonitorStateChangesAsync(cancellationToken))
            {
                if (!deploymentState.Annotations.ContainsKey("churrostack.com/deployment-id") &&
                    !deploymentState.Annotations.ContainsKey("churrostack.com/app-id"))
                    continue;

                var deploymentName = deploymentState.Annotations["churrostack.com/deployment-id"];
                var appName = deploymentState.Annotations["churrostack.com/app-id"];

                try
                {
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

                    bool changedApp = false;

                    var deployment = await dbContext.Set<ApplicationDeployment>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.ApplicationId == appId && o.Name == deploymentName);

                    if (deployment is not null)
                    {
                        if (deploymentState.Replicas > 0)
                        {
                            deployment.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned;
                            if (deploymentState.Replicas == deploymentState.Available)
                            {
                                if (deployment.ExecutionStatus != Models.Dtos.Deployment.DeploymentExecutionStatus.Running)
                                {
                                    changedApp = true;
                                    deployment.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Running;
                                }
                            }
                            else
                            {
                                if (deployment.ExecutionStatus != Models.Dtos.Deployment.DeploymentExecutionStatus.Starting)
                                {
                                    changedApp = true;
                                    if (deployment.ProvisionStatus != Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned)
                                    {
                                        deployment.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned;
                                    }
                                    deployment.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Starting;
                                }
                            }
                        }
                        else
                        {
                            if (deploymentState.Available > deploymentState.Replicas)
                            {
                                if (deployment.ExecutionStatus != Models.Dtos.Deployment.DeploymentExecutionStatus.Stopping)
                                {
                                    changedApp = true;
                                    deployment.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Stopping;
                                }
                            }
                            else
                            {
                                if (deployment.ExecutionStatus != Models.Dtos.Deployment.DeploymentExecutionStatus.Stopped)
                                {
                                    changedApp = true;
                                    deployment.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Stopped;
                                }
                                if (deployment.ProvisionStatus == Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioning)
                                {
                                    changedApp = true;
                                    deployment.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned;
                                }
                            }
                        }

                        var appStatusChanged = changedApp;

                        if (deployment.DeploymentStatus is null || deployment.DeploymentStatus.Annotations == null ||
                            !deployment.DeploymentStatus.Annotations.SequenceEqual(deploymentState.Annotations) ||
                            deployment.DeploymentStatus.Replicas != deploymentState.Replicas ||
                            deployment.DeploymentStatus.Available != deploymentState.Available)
                        {
                            var conn = dbContext.Database.GetDbConnection();
                            var isConnOpen = conn.State == ConnectionState.Open;
                            if (!isConnOpen)
                                await conn.OpenAsync(cancellationToken);
                            try
                            {
                                using var cmd = conn.CreateCommand();
                                cmd.CommandText = "UPDATE cs.application_deployment SET provision_status = @provisionStatus, execution_status = @executionStatus WHERE account_id = @accountId AND application_id = @appId AND name = @deploymentName";
                                cmd.Parameters.Add(new Npgsql.NpgsqlParameter("@provisionStatus", (int)deployment.ProvisionStatus));
                                cmd.Parameters.Add(new Npgsql.NpgsqlParameter("@executionStatus", (int)deployment.ExecutionStatus));
                                cmd.Parameters.Add(new Npgsql.NpgsqlParameter("@accountId", accountId));
                                cmd.Parameters.Add(new Npgsql.NpgsqlParameter("@appId", appId));
                                cmd.Parameters.Add(new Npgsql.NpgsqlParameter("@deploymentName", deploymentName));
                                await cmd.ExecuteNonQueryAsync();
                            }
                            finally
                            {
                                if (!isConnOpen)
                                    await conn.CloseAsync();
                            }
                        }

                        if (changedApp)
                        {
                            await _clientNotificationService.NotifyChangeAsync(accountId, appName, ClientNotificationService.NotificationTargetType.Application, cancellationToken);
                            await _clientNotificationService.NotifyChangeAsync(accountId, deployment.Name, ClientNotificationService.NotificationTargetType.Deployment, cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log and ignore
                    _logger.LogError(ex, $"Error processing deployment change for app '{appName}' in tenant {accountId}.");
                }
            }
        }
    }
}
