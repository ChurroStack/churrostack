using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Entra;
using DispatchR;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class GetEntraSyncStatusHandler : IRequestHandler<GetEntraSyncStatus, ValueTask<EntraSyncStatus>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;

        public GetEntraSyncStatusHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IMediator mediator, ICacheService cacheService)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
            _cacheService = cacheService;
        }

        public async ValueTask<EntraSyncStatus> Handle(GetEntraSyncStatus request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var status = await _cacheService.GetAsync<EntraSyncStatus>(EntraSyncConfigStore.StatusKey(_tenantResolver.AccountId), cancellationToken);
            return EntraSyncConfigStore.Normalize(status, DateTimeOffset.UtcNow);
        }
    }
}
