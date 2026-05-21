using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityAclsHandler : IRequestHandler<GetIdentityAcls, ValueTask<IDictionary<long, Permission>>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ITenantResolver _tenantResolver;

        public GetIdentityAclsHandler(ChurrosDbContext dbContext, ICacheService cacheService, ITenantResolver tenantResolver)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<IDictionary<long, Permission>> Handle(GetIdentityAcls request, CancellationToken cancellationToken)
        {
            return await _cacheService.GetOrAddAsync($"tenant:{_tenantResolver.AccountId}:identity:{request.IdentityId}:{request.Permission}:acls", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var userGroupsIds = await _dbContext.Set<Domain.IdentityMemberOf>()
                    .AsNoTracking()
                    .Where(o => o.IdentityId == request.IdentityId)
                    .Select(o => o.GroupId)
                    .ToArrayAsync();
                userGroupsIds = userGroupsIds.Union([request.IdentityId]).ToArray();
                var aclPermissions = await _dbContext.ExecuteQueryAsync<AclPermissionItem>($@"
                    SELECT acl_id, bit_or(permission) AS permission
                    FROM cs.acl_member
                    WHERE account_id = {_tenantResolver.AccountId} AND identity_id = ANY ({userGroupsIds}) AND (permission & {request.Permission}) = {request.Permission}
                    GROUP BY acl_id
                ");
                return aclPermissions.ToDictionary(o => o.AclId, o => o.Permission);
            }, cancellationToken);
        }
    }
}
