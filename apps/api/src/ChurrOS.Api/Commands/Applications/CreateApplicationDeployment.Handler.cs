using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class CreateApplicationDeploymentHandler : IRequestHandler<CreateApplicationDeployment, ValueTask<DeploymentSummary?>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;

        public CreateApplicationDeploymentHandler(IMediator mediator, ChurrosDbContext context)
        {
            _mediator = mediator;
            _context = context;
        }

        public async ValueTask<DeploymentSummary?> Handle(CreateApplicationDeployment request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Environment)
                .Include(o => o.Deployments)
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to create new deployments.");
            }

            var identity = await _mediator.Send(new GetIdentity(request.Body.IdentityName), cancellationToken);

            if (app.Deployments?.Any(o => o.OwnerId == identity.Id) == true)
            {
                throw new HttpException(400, $"Deployment for identity '{request.Body.IdentityName}' already exists.");
            }

            var appMember = await _context.Set<Domain.AclMember>()
                .Where(o => o.AclId == app.AclId && o.IdentityId == identity.Id)
                .SingleOrDefaultAsync();

            if (appMember == null)
            {
                _context.Set<Domain.AclMember>().Add(new Domain.AclMember(app.AccountId, app.AclId, identity.Id, Permission.Execute));
                await _context.SaveChangesAsync();
            }

            var result = await _mediator.Send(new DeployApplication(app.Name, deploymentOwnerId: identity.Id), cancellationToken);

            return result?.FirstOrDefault();
        }
    }
}
