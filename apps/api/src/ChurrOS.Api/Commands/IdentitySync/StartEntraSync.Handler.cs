using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Entra;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class StartEntraSyncHandler : IRequestHandler<StartEntraSync, ValueTask<EntraSyncStatus>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StartEntraSyncHandler> _logger;

        public StartEntraSyncHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IMediator mediator, ICacheService cacheService, ISchedulerFactory schedulerFactory, IConfiguration configuration, ILogger<StartEntraSyncHandler> logger)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
            _cacheService = cacheService;
            _schedulerFactory = schedulerFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async ValueTask<EntraSyncStatus> Handle(StartEntraSync request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var accountId = _tenantResolver.AccountId;
            var now = DateTimeOffset.UtcNow;

            if (!GraphDirectoryClient.IsAvailable(_configuration))
                throw new ArgumentException("Entra sync is not configured on the server (ExternalProviders:Microsoft ClientId, ClientSecret and TenantId).");

            var current = EntraSyncConfigStore.Normalize(
                await _cacheService.GetAsync<EntraSyncStatus>(EntraSyncConfigStore.StatusKey(accountId), cancellationToken), now);
            if (EntraSyncConfigStore.IsActive(current))
            {
                _logger.LogInformation("[EntraSync] accountId={AccountId} outcome=start_rejected reason=already_{State}", accountId, current.State);
                throw new HttpException(409, "An Entra sync is already running.", current.RunId);
            }

            var queued = new EntraSyncStatus
            {
                RunId = Guid.NewGuid().ToString("N")[..12],
                State = EntraSyncState.Queued,
                Phase = "Queued",
                Trigger = "manual",
                StartedAt = now,
                HeartbeatAt = now
            };
            await _cacheService.InvalidateAsync(EntraSyncConfigStore.CancelKey(accountId));
            await _cacheService.SaveAsync(EntraSyncConfigStore.StatusKey(accountId), queued, TimeSpan.FromDays(30), cancellationToken);

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await EntraSyncScheduling.TriggerNowAsync(scheduler, accountId, cancellationToken);

            _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} trigger=manual outcome=queued", accountId, queued.RunId);
            return queued;
        }
    }
}
