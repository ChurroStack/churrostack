using ChurrOS.Api.Data;
using ChurrOS.Api.Services;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityGroupsIdsHandler : IRequestHandler<GetIdentityGroupsIds, ValueTask<long[]>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ITenantResolver _tenantResolver;

        public GetIdentityGroupsIdsHandler(ChurrosDbContext dbContext, ICacheService cacheService, ITenantResolver tenantResolver)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<long[]> Handle(GetIdentityGroupsIds request, CancellationToken cancellationToken)
        {
            var groupIds = await _cacheService.GetOrAddAsync($"tenant:{_tenantResolver.AccountId}:identity:{request.IdentityId}:groups", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                return await _dbContext.Set<Domain.IdentityMemberOf>()
                    .AsNoTracking()
                    .Where(o => o.IdentityId == request.IdentityId)
                    .Select(o => o.GroupId)
                    .ToArrayAsync();
            }, cancellationToken);
            if (request.IncludeSelf)
            {
                return groupIds.Union([request.IdentityId]).ToArray();
            }
            return groupIds;
        }
    }
}
