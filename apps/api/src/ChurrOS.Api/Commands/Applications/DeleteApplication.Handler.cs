using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeleteApplicationHandler : IRequestHandler<DeleteApplication, Task>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly RunnerService _runnerService;
        private readonly ICacheService _cacheService;

        public DeleteApplicationHandler(IMediator mediator, ChurrosDbContext context, RunnerService runnerService, ICacheService cacheService)
        {
            _mediator = mediator;
            _context = context;
            _runnerService = runnerService;
            _cacheService = cacheService;
        }

        public async Task Handle(DeleteApplication request, CancellationToken cancellationToken)
        {
            var app = await _context.Set<Domain.Application>()
                .AsNoTracking()
                .Include(o => o.Environment)
                .Where(o => o.Name == request.Name)
                .Select(o => new { o.Id, o.Name, o.AclId, o.Environment, o.Deployments, o.Ports })
                .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Manage), cancellationToken);
                if (!identityAcls.ContainsKey(app.Environment!.AclId) && !identityAcls.ContainsKey(app.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to delete this application in this environment.");
            }

            if (!string.IsNullOrWhiteSpace(request.DeploymentName))
            {
                var deployment = app.Deployments?.FirstOrDefault(o => o.Name == request.DeploymentName);
                if (deployment == null)
                {
                    throw new NotFoundException($"Deployment with name '{request.DeploymentName}' was not found in application '{request.Name}'.");
                }

                // Deallocate deployment.
                if (deployment.ProvisionStatus != Models.Dtos.Deployment.DeploymentProvisionStatus.Pending)
                {
                    var parts = app.Environment?.EncryptionKey.Split(':') ?? [];
                    var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
                    var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);
                    await client.DeleteAsync(deployment.Name, cancellationToken);
                }

                await _context.Set<Domain.ApplicationDeployment>().Where(o => o.ApplicationId == deployment.ApplicationId && o.Name == deployment.Name).ExecuteDeleteAsync(cancellationToken);

                await _cacheService.InvalidatePrefixAsync($"app:{app.Name}");

                return;
            }

            // TODO: Deallocate everything related to this application
            foreach (var deployment in app.Deployments ?? [])
            {
                // Deallocate deployments.
                if (deployment.ProvisionStatus != Models.Dtos.Deployment.DeploymentProvisionStatus.Pending)
                {
                    var parts = app.Environment?.EncryptionKey.Split(':') ?? [];
                    var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
                    var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);
                    await client.DeleteAsync(deployment.Name, cancellationToken);
                }
            }

            // Delete related schedules
            var schedulerRepo = _context.Set<Domain.ApplicationSchedule>();
            var schedules = await schedulerRepo.Where(o => o.ApplicationId == app.Id).Select(o => o.Name).ToListAsync();
            foreach (var schedule in schedules)
            {
                await _mediator.Send(new DeleteApplicationSchedule(app.Name, schedule), cancellationToken);
            }

            await _context.Set<Domain.Application>().Where(o => o.Name == request.Name).ExecuteDeleteAsync(cancellationToken);
            await _context.Set<Acl>().Where(o => o.Id == app.AclId).ExecuteDeleteAsync(cancellationToken);

            await _cacheService.InvalidatePrefixAsync($"app:{app.Name}");
        }
    }
}
