using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using LazyCache;
using Microsoft.Extensions.Logging;

namespace ChurrOS.Api.Services.AutoStart
{
    /// <summary>
    /// Called from the YARP /share/* pipeline. Returns true when the request should be
    /// forwarded; returns false when the response has already been written (cooldown,
    /// hold-timeout, or unrecoverable error) and the caller should NOT call next().
    /// </summary>
    public sealed class AutoStartTransform
    {
        private readonly AutoStartCache _autoStartCache;
        private readonly AutoStartCoordinator _coordinator;
        private readonly IAppCache _processCache;
        private readonly ILogger<AutoStartTransform> _logger;

        public AutoStartTransform(
            AutoStartCache autoStartCache,
            AutoStartCoordinator coordinator,
            IAppCache processCache,
            ILogger<AutoStartTransform> logger)
        {
            _autoStartCache = autoStartCache;
            _coordinator = coordinator;
            _processCache = processCache;
            _logger = logger;
        }

        public async Task<bool> HandleShareRequestAsync(HttpContext context, string appName, long accountId)
        {
            var dbContext = context.RequestServices.GetRequiredService<ChurrosDbContext>();
            var route = await _autoStartCache.GetRouteAsync(appName, dbContext, context.RequestAborted);
            if (route is null)
            {
                return true;
            }

            // Workspace apps have per-user deployments; auto-start/stop semantics are not
            // defined for them in V1 (the running flag and inflight key are app-scoped, which
            // would conflate per-user deployment state). Pass through untouched.
            if (route.Mode == ApplicationMode.Workspace)
            {
                return true;
            }

            if (route.ExecutionStatus == DeploymentExecutionStatus.Running)
            {
                if (route.AutoStopEnabled)
                {
                    await TouchLastActivityAsync(route.AppId);
                }
                return true;
            }

            if (!route.AutoStartEnabled)
            {
                return true;
            }

            var outcome = await _coordinator.HoldUntilRunningAsync(appName, route.AppId, accountId, context.RequestAborted);
            switch (outcome)
            {
                case HoldOutcome.Running:
                    if (route.AutoStopEnabled)
                    {
                        await TouchLastActivityAsync(route.AppId);
                    }
                    await _autoStartCache.InvalidateRouteAsync(appName);
                    return true;
                case HoldOutcome.Cooldown:
                    await WriteJsonAsync(context, StatusCodes.Status503ServiceUnavailable,
                        "Application is in auto-stop cooldown; try again in a moment.");
                    return false;
                case HoldOutcome.Timeout:
                    await WriteJsonAsync(context, StatusCodes.Status504GatewayTimeout,
                        "Application did not become ready in time.");
                    return false;
                default:
                    await WriteJsonAsync(context, StatusCodes.Status503ServiceUnavailable,
                        "Auto-start unavailable; please retry.");
                    return false;
            }
        }

        private async Task TouchLastActivityAsync(long appId)
        {
            // Atomic per-process throttle: GetOrAdd guarantees the factory runs at most once
            // per key within the cache window. We claim a one-shot sentinel so only the
            // first caller per replica per 30 s issues the Redis write; the rest see a
            // sentinel that was already claimed and return immediately.
            var key = $"la_throttle:{appId}";
            var firstWriter = _processCache.GetOrAdd(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = AutoStartConstants.LastActivityThrottle;
                return new ThrottleSentinel();
            });
            if (!firstWriter.Claim()) return;

            try
            {
                await _autoStartCache.WriteLastActivityAsync(appId, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AutoStart] last_activity write failed app={Id}", appId);
            }
        }

        private sealed class ThrottleSentinel
        {
            private int _claimed;
            public bool Claim() => Interlocked.Exchange(ref _claimed, 1) == 0;
        }

        private static Task WriteJsonAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
