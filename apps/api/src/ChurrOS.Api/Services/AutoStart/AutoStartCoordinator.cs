using ChurrOS.Api.Commands.Applications;
using DispatchR;
using Microsoft.Extensions.Logging;

namespace ChurrOS.Api.Services.AutoStart
{
    public enum HoldOutcome { Running, Cooldown, Timeout, Error }

    public sealed class AutoStartCoordinator
    {
        private readonly AutoStartCache _autoStartCache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<AutoStartCoordinator> _logger;

        public AutoStartCoordinator(
            AutoStartCache autoStartCache,
            IServiceScopeFactory scopeFactory,
            IHostApplicationLifetime lifetime,
            ILogger<AutoStartCoordinator> logger)
        {
            _autoStartCache = autoStartCache;
            _scopeFactory = scopeFactory;
            _lifetime = lifetime;
            _logger = logger;
        }

        public async Task<HoldOutcome> HoldUntilRunningAsync(string appName, long appId, long accountId, CancellationToken cancellationToken, bool bypassCooldown = false)
        {
            // Scheduled jobs (ApplicationHttpRequestJob) are system-initiated and must run
            // regardless of cooldown — the user explicitly scheduled the request, and the
            // cooldown is a flap guard against client-driven request loops, not a global
            // pause. Passing through bypassCooldown skips that check.
            if (!bypassCooldown && await _autoStartCache.IsInCooldownAsync(appId))
            {
                _logger.LogInformation("[AutoStart] cooldown skip app={App} id={Id}", appName, appId);
                return HoldOutcome.Cooldown;
            }

            var isLeader = await _autoStartCache.TryClaimStartAsync(appId);
            if (isLeader)
            {
                _logger.LogInformation("[AutoStart] leader app={App} id={Id} tenant={Tenant}", appName, appId, accountId);
                _ = Task.Run(() => FireStartAsync(appName, appId, accountId), _lifetime.ApplicationStopping);
            }
            else
            {
                _logger.LogDebug("[AutoStart] follower app={App} id={Id}", appName, appId);
            }

            var deadline = DateTimeOffset.UtcNow + AutoStartConstants.HoldTimeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (cancellationToken.IsCancellationRequested) return HoldOutcome.Error;
                if (await _autoStartCache.IsRunningAsync(appId))
                {
                    _logger.LogDebug("[AutoStart] ready app={App} id={Id}", appName, appId);
                    return HoldOutcome.Running;
                }
                try
                {
                    await Task.Delay(AutoStartConstants.PollInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return HoldOutcome.Error;
                }
            }

            _logger.LogWarning("[AutoStart] timeout app={App} id={Id} after={Sec}s", appName, appId, AutoStartConstants.HoldTimeout.TotalSeconds);
            return HoldOutcome.Timeout;
        }

        private async Task FireStartAsync(string appName, long appId, long accountId)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
                tenantResolver.SetAccountId(accountId);
                tenantResolver.SetIdentity("system");

                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new StartApplication(appName) { BypassAcl = true }, _lifetime.ApplicationStopping);
                _logger.LogInformation("[AutoStart] start dispatched app={App} id={Id}", appName, appId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AutoStart] start failed app={App} id={Id}", appName, appId);
            }
        }
    }
}
