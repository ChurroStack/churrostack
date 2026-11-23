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
    public class StopEntraSyncHandler : IRequestHandler<StopEntraSync, ValueTask<EntraSyncStatus>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;
        private readonly ILogger<StopEntraSyncHandler> _logger;

        public StopEntraSyncHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IMediator mediator, ICacheService cacheService, ILogger<StopEntraSyncHandler> logger)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async ValueTask<EntraSyncStatus> Handle(StopEntraSync request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var accountId = _tenantResolver.AccountId;
            var status = EntraSyncConfigStore.Normalize(
                await _cacheService.GetAsync<EntraSyncStatus>(EntraSyncConfigStore.StatusKey(accountId), cancellationToken), DateTimeOffset.UtcNow);

            if (!EntraSyncConfigStore.IsActive(status))
                return status;

            // The job may run on another node (clustered Quartz), so cancellation is a Redis flag polled by the runner between pages/groups.
            await _cacheService.SaveAsync(EntraSyncConfigStore.CancelKey(accountId), DateTimeOffset.UtcNow.ToString("O"), TimeSpan.FromHours(1), cancellationToken);
            _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} outcome=stop_requested", accountId, status.RunId);
            return status;
        }
    }
}
