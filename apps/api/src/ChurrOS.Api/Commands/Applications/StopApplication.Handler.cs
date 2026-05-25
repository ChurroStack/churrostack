using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.AutoStart;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class StopApplicationHandler : IRequestHandler<StopApplication, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly RunnerService _runnerService;
        private readonly AutoStartCache _autoStartCache;

        public StopApplicationHandler(
            IMediator mediator,
            ChurrosDbContext context,
            RunnerService runnerService,
            AutoStartCache autoStartCache)
        {
            _mediator = mediator;
            _context = context;
            _runnerService = runnerService;
            _autoStartCache = autoStartCache;
        }

        public async Task Handle(StopApplication request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Environment)
                .Include(o => o.Deployments)
                .Select(o => new { o.Id, o.Name, o.AclId, o.Environment, o.Mode, o.Size, o.Deployments })
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            if (app.Deployments is null || app.Deployments.Count == 0)
            {
                throw new NotFoundException($"Application with name '{request.Name}' is not deployed yet.");
            }

            if (!request.BypassAcl)
            {
                var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
                if (!isAdmin)
                {
                    var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? Permission.Read : Permission.Write), cancellationToken);
                    if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                        throw new UnauthorizedAccessException("You do not have permission to stop this application.");
                }
            }

            var parts = app.Environment!.EncryptionKey.Split(':');
            var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
            var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);

            if (!string.IsNullOrWhiteSpace(request.DeploymentName))
            {
                var deployment = app.Deployments.FirstOrDefault(o => o.Name == request.DeploymentName);
                if (deployment == null)
                {
                    throw new NotFoundException($"Deployment with name '{request.DeploymentName}' was not found for application '{request.Name}'.");
                }
                await client.StopAsync(deployment.Name, cancellationToken);
            }
            else
            {
                var deploymentName = app.Mode == Models.Dtos.Application.ApplicationMode.Workspace ? $"{app.Name}-{_context.IdentityId}" : app.Name;
                await client.StopAsync(deploymentName, cancellationToken);
            }

            await _autoStartCache.InvalidateRouteAsync(app.Name);
            await _autoStartCache.ClearRunningAsync(app.Id);

            // Cooldown only makes sense for system-initiated stops (auto-stop), where we
            // want to keep the app from being re-started by an immediate request. Pairing
            // it with BypassAcl here means a hypothetical future caller can't accidentally
            // impose a cooldown on a manual stop and silently break the "manual start
            // always works" invariant.
            if (request.SetCooldown && request.BypassAcl)
            {
                await _autoStartCache.SetCooldownAsync(app.Id);
            }
        }
    }
}
