using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class StartApplicationHandler : IRequestHandler<StartApplication, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly RunnerService _runnerService;
        private readonly QuotaService _quotaService;
        private readonly ILockService _lockService;
        private readonly ITenantResolver _tenantResolver;

        public StartApplicationHandler(
            IMediator mediator,
            ChurrosDbContext context,
            RunnerService runnerService,
            QuotaService quotaService,
            ILockService lockService,
            ITenantResolver tenantResolver)
        {
            _mediator = mediator;
            _context = context;
            _runnerService = runnerService;
            _quotaService = quotaService;
            _lockService = lockService;
            _tenantResolver = tenantResolver;
        }

        public async Task Handle(StartApplication request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Environment)
                .Include(o => o.Deployments)
                .Select(o => new { o.Id, o.Name, o.Mode, o.AclId, o.EnvironmentId, o.Environment, o.Size, o.Deployments })
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            if (app.Deployments is null || app.Deployments.Count == 0)
            {
                throw new NotFoundException($"Application with name '{request.Name}' is not deployed yet.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? Permission.Read : Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to start this application.");
            }

            var parts = app.Environment!.EncryptionKey.Split(':');
            var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
            var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);

            // Serialize the quota check + start against this environment so two concurrent
            // starts can't both pass the budget check before either's ExecutionStatus updates.
            var lockKey = $"churros_tenant:{_tenantResolver.AccountId}:env:{app.EnvironmentId}:resource_lock";
            await using var handle = await _lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(5), cancellationToken)
                ?? throw new InvalidOperationException("Environment is busy, please retry.");

            await _mediator.Send(new EnsureEnvironmentRunningQuota(app.EnvironmentId, app.Id, app.Size, EnsureRunningQuotaMode.Start), cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.DeploymentName))
            {
                var deployment = app.Deployments.FirstOrDefault(o => o.Name == request.DeploymentName);
                if (deployment == null)
                {
                    throw new NotFoundException($"Deployment with name '{request.DeploymentName}' was not found for application '{request.Name}'.");
                }
                await client.StartAsync(deployment.Name, cancellationToken);
            }
            else
            {
                var deploymentName = app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? $"{app.Name}-{_context.IdentityId}" : app.Name;
                await client.StartAsync(deploymentName, cancellationToken);
            }
        }
    }
}
