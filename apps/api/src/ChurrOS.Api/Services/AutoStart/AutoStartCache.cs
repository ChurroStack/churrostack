using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChurrOS.Api.Services.AutoStart
{
    public sealed record ShareRouteInfo(long AppId, ApplicationMode Mode, DeploymentExecutionStatus ExecutionStatus, bool AutoStartEnabled, bool AutoStopEnabled);

    public sealed class AutoStartCache
    {
        private readonly ICacheService _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<AutoStartCache> _logger;

        public AutoStartCache(ICacheService cache, IConnectionMultiplexer redis, ILogger<AutoStartCache> logger)
        {
            _cache = cache;
            _redis = redis;
            _logger = logger;
        }

        public Task<ShareRouteInfo?> GetRouteAsync(string appName, ChurrosDbContext dbContext, CancellationToken cancellationToken)
        {
            return _cache.GetOrAddAsync<ShareRouteInfo?>(AutoStartConstants.RouteCacheKey(appName), async ctx =>
            {
                ctx.SetAbsoluteExpiration(AutoStartConstants.RouteCacheTtl);

                var row = await dbContext.Set<Domain.Application>()
                    .AsNoTracking()
                    .Where(o => o.Name == appName)
                    .Select(o => new
                    {
                        o.Id,
                        o.Mode,
                        o.Metadata,
                        ExecutionStatus = o.Deployments!
                            .Select(d => (DeploymentExecutionStatus?)d.ExecutionStatus)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (row is null) return null;
                var settings = AutoStartSettings.From(row.Metadata);
                return new ShareRouteInfo(
                    row.Id,
                    row.Mode,
                    row.ExecutionStatus ?? DeploymentExecutionStatus.Stopped,
                    settings.AutoStartEnabled,
                    settings.AutoStopEnabled);
            }, cancellationToken);
        }

        public Task InvalidateRouteAsync(string appName)
        {
            _logger.LogDebug("[AutoStartCache] invalidate route app={App}", appName);
            return _cache.InvalidateAsync(AutoStartConstants.RouteCacheKey(appName));
        }

        public Task SetRunningAsync(long appId)
        {
            return _redis.GetDatabase().StringSetAsync(
                AutoStartConstants.RunningKey(appId), "1",
                AutoStartConstants.RunningTtl);
        }

        public Task ClearRunningAsync(long appId)
        {
            return _redis.GetDatabase().KeyDeleteAsync(AutoStartConstants.RunningKey(appId));
        }

        public Task<bool> IsRunningAsync(long appId)
        {
            return _redis.GetDatabase().KeyExistsAsync(AutoStartConstants.RunningKey(appId));
        }

        public Task<bool> IsInCooldownAsync(long appId)
        {
            return _redis.GetDatabase().KeyExistsAsync(AutoStartConstants.CooldownKey(appId));
        }

        public Task SetCooldownAsync(long appId)
        {
            return _redis.GetDatabase().StringSetAsync(
                AutoStartConstants.CooldownKey(appId), "1",
                AutoStartConstants.CooldownTtl);
        }

        public Task ClearCooldownAsync(long appId)
        {
            return _redis.GetDatabase().KeyDeleteAsync(AutoStartConstants.CooldownKey(appId));
        }

        public Task<bool> TryClaimStopAsync(long appId)
        {
            return _redis.GetDatabase().StringSetAsync(
                AutoStartConstants.AutoStopInflightKey(appId), "1",
                AutoStartConstants.AutoStopInflightTtl,
                When.NotExists);
        }

        public Task<bool> TryClaimStartAsync(long appId)
        {
            return _redis.GetDatabase().StringSetAsync(
                AutoStartConstants.InflightKey(appId), "1",
                AutoStartConstants.InflightTtl,
                When.NotExists);
        }

        public Task WriteLastActivityAsync(long appId, DateTimeOffset now)
        {
            return _redis.GetDatabase().StringSetAsync(
                AutoStartConstants.LastActivityKey(appId),
                now.ToString("O"),
                AutoStartConstants.LastActivityTtl);
        }

        public async Task<DateTimeOffset?> GetLastActivityAsync(long appId)
        {
            var raw = await _redis.GetDatabase().StringGetAsync(AutoStartConstants.LastActivityKey(appId));
            if (raw.IsNullOrEmpty) return null;
            return DateTimeOffset.TryParse(raw.ToString(), out var parsed) ? parsed : null;
        }
    }
}
