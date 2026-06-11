using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetAllowedHostPathsHandler : IRequestHandler<GetAllowedHostPaths, ValueTask<AllowedHostPathItem[]>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;

        public GetAllowedHostPathsHandler(ChurrosDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async ValueTask<AllowedHostPathItem[]> Handle(GetAllowedHostPaths request, CancellationToken cancellationToken)
        {
            var environment = await _context
                .Set<Domain.Environment>()
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Name == request.EnvironmentName, cancellationToken);

            if (environment is null)
            {
                throw new NotFoundException($"Environment with name '{request.EnvironmentName}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(environment.AclId, Permission.Read), cancellationToken))
            {
                throw new UnauthorizedAccessException();
            }

            var hostPaths = environment.Definition?.HostPaths;
            if (hostPaths is null || hostPaths.Length == 0)
            {
                return [];
            }

            var principals = await ResolvePrincipalsAsync(cancellationToken);

            return hostPaths
                .Where(hp => hp.Allowed is not null && hp.Allowed.Any(a => principals.Contains(a)))
                .Select(hp => new AllowedHostPathItem(hp.Path, hp.Title))
                .ToArray();
        }

        /// <summary>
        /// The current user's identity name plus the names of every group they belong
        /// to, compared case-insensitively against the YAML allow-list.
        /// </summary>
        private async Task<HashSet<string>> ResolvePrincipalsAsync(CancellationToken cancellationToken)
        {
            var identityRepo = _context.Set<Domain.Identity>();
            var principals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var selfName = await identityRepo
                .AsNoTracking()
                .Where(o => o.Id == _context.IdentityId)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(selfName))
            {
                principals.Add(selfName);
            }

            var groupIds = await _mediator.Send(new GetIdentityGroupsIds(_context.IdentityId), cancellationToken);
            if (groupIds.Length > 0)
            {
                var groupNames = await identityRepo
                    .AsNoTracking()
                    .Where(o => groupIds.Contains(o.Id))
                    .Select(o => o.Name)
                    .ToArrayAsync(cancellationToken);
                foreach (var groupName in groupNames)
                {
                    principals.Add(groupName);
                }
            }

            return principals;
        }
    }
}
