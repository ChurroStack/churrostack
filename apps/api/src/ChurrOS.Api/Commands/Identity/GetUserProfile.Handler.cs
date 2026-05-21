using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetUserProfileHandler : IRequestHandler<GetUserProfile, ValueTask<ProfileItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly IMediator _mediator;

        public GetUserProfileHandler(ChurrosDbContext dbContext, IMediator mediator)
        {
            _dbContext = dbContext;
            _mediator = mediator;
        }

        public async ValueTask<ProfileItem> Handle(GetUserProfile request, CancellationToken cancellationToken)
        {
            var identity = await _dbContext.Set<Domain.Identity>()
                .AsNoTracking()
                .Include(o => o.Account)
                .Include("MemberOf.Group")
                .Where(o => o.Name.Equals(request.IdentityName))
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

            if (identity is null)
                throw new NotFoundException($"Identity '{request.IdentityName}' was not found.");

            var memberOf = identity!.MemberOf?.Select(o => o.Group!.Name)?.ToArray() ?? [];

            var userRole = _dbContext.Owners.Any(o => o.Equals(request.IdentityName, StringComparison.InvariantCultureIgnoreCase)) ? IdentityRole.Administrator : identity.Role;

            var displayName = identity.DisplayName;
            if (identity.Name.Equals(displayName))
            {
                var claimName = request.Claims.FirstOrDefault(o => o.Type == "name")?.Value;
                if (!identity.Name.Equals(claimName))
                    displayName = claimName;
            }

            var identityAcls = await _mediator.Send(new GetIdentityAcls(_dbContext.IdentityId, Permission.Execute), cancellationToken);
            var canCreateApps = (userRole == IdentityRole.Administrator) || _dbContext.Set<Domain.Environment>().AsNoTracking().Any(o => identityAcls.Keys.Contains(o.AclId));

            return new ProfileItem(identity.Name, displayName ?? identity.DisplayName, identity.Account?.Name ?? "", userRole, memberOf, canCreateApps, identity.Properties.Claims, identity.Properties.Metadata);
        }
    }
}
