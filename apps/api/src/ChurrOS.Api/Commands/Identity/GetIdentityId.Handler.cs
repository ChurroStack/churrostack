using ChurrOS.Api.Data;
using ChurrOS.Api.Services;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityIdHandler : IRequestHandler<GetIdentityId, ValueTask<long>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;

        public GetIdentityIdHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, ICacheService cacheService)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
        }

        public async ValueTask<long> Handle(GetIdentityId request, CancellationToken cancellationToken)
        {
            return await _cacheService.GetOrAddAsync($"tenant:{_tenantResolver.AccountId}:identity:{request.Name}:id", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                return await _dbContext.Set<Domain.Identity>()
                    .AsNoTracking()
                    .Where(o => o.Name.ToLower().Equals(request.Name.ToLower()))
                    .Select(o => o.Id)
                    .FirstOrDefaultAsync(cancellationToken: cancellationToken);
            }, cancellationToken);
        }
    }
}
