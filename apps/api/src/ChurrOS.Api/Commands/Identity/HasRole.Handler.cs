using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class HasRoleHandler : IRequestHandler<HasRole, ValueTask<bool>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;

        public HasRoleHandler(ITenantResolver tenantResolver, ChurrosDbContext dbContext, ICacheService cacheService, IMediator mediator)
        {
            _tenantResolver = tenantResolver;
            _dbContext = dbContext;
            _cacheService = cacheService;
            _mediator = mediator;
        }

        public async ValueTask<bool> Handle(HasRole request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            var identityId = request.IdentityId;

            if (identityId == _dbContext.IdentityId && _dbContext.Owners.Contains(_tenantResolver.Identity?.Name ?? ""))
                return true;

            var hasRole = false;
            var cackeKey = $"tenant:{_tenantResolver.AccountId}:identity:{identityId}:identites";

            var userIdentities = await _cacheService.GetOrAddAsync(cackeKey, async (ctx) =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var userGroupsIds = await _mediator.Send(new GetIdentityGroupsIds(identityId), cancellationToken);

                var repo = _dbContext.Set<Domain.Identity>().AsNoTracking();
                return await repo.Where(o => userGroupsIds.Contains(o.Id) || o.Id == identityId).ToListAsync();
            }, cancellationToken);

            var query = request.Role switch
            {
                IdentityRole.Administrator => userIdentities.Where(o => o.Role == IdentityRole.Administrator),
                IdentityRole.User => userIdentities.Where(o => o.Role == IdentityRole.User || o.Role == IdentityRole.Administrator),
                _ => throw new InvalidOperationException(LocalizationService.GetString("InvalidRole"))
            };

            hasRole = query.Any();
            return hasRole;
        }
    }
}
