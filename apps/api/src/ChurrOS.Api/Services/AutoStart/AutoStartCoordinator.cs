using ChurrOS.Api.Commands.Applications;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using Microsoft.Extensions.Logging;

namespace ChurrOS.Api.Services.AutoStart
{
    public enum HoldOutcome { Running, Cooldown, Timeout, Error, Rejected }

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

            // If the app is already running (e.g. a manual start just completed), forward
            // immediately — this also avoids rejecting on a now-stale start_failed key.
            if (await _autoStartCache.IsRunningAsync(appId))
            {
                return HoldOutcome.Running;
            }

            // Backoff guard: a recent start attempt failed (e.g. quota exceeded). Do not claim
            // leadership or poll — short-circuit to a fast rejection while the window lives. A
            // later manual/system start clears this key (see StartApplicationHandler), so an
            // in-progress legitimate start lifts the backoff rather than being rejected.
            if (await _autoStartCache.GetStartFailedAsync(appId) is not null)
            {
                _logger.LogInformation("[AutoStart] rejected (recent failure) app={App} id={Id}", appName, appId);
                return HoldOutcome.Rejected;
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
                // The leader may have failed admission while we were holding — stop fast.
                if (await _autoStartCache.GetStartFailedAsync(appId) is not null)
                {
                    _logger.LogInformation("[AutoStart] rejected (start failed) app={App} id={Id}", appName, appId);
                    return HoldOutcome.Rejected;
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
                // Surface the failure to holders/new requests as a fast rejection instead of letting
                // them poll to the 300s HoldTimeout. Only EnvironmentCapacityException carries a
                // vetted, user-safe reason (quota exceeded / env busy); every other exception —
                // including the many framework types that derive from InvalidOperationException —
                // gets a generic message so internal text never reaches the response body. Set the
                // backoff key BEFORE releasing the inflight claim so no racing caller claims
                // leadership in between.
                _logger.LogError(ex, "[AutoStart] start failed app={App} id={Id}", appName, appId);
                var isCapacity = ex is EnvironmentCapacityException;
                var reason = isCapacity ? ex.Message : "Application failed to start.";
                var ttl = isCapacity ? AutoStartConstants.StartFailedTtl : AutoStartConstants.StartFailedHardTtl;

                // Record the backoff first, then release the inflight claim — independently, so a
                // transient Redis error on one still lets the other run. Clearing inflight is the
                // one that must happen: otherwise the claim lingers for its full TTL and blocks
                // every retry while holders poll to the 300 s timeout.
                try { await _autoStartCache.SetStartFailedAsync(appId, reason, ttl); }
                catch (Exception e) { _logger.LogWarning(e, "[AutoStart] start_failed write failed app={App} id={Id}", appName, appId); }
                try { await _autoStartCache.ClearInflightAsync(appId); }
                catch (Exception e) { _logger.LogWarning(e, "[AutoStart] inflight clear failed app={App} id={Id}", appName, appId); }
            }
        }
    }
}
