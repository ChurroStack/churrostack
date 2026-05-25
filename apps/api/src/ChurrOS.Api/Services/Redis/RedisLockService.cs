using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChurrOS.Api.Services.Redis
{
    public class RedisLockService : ILockService
    {
        private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(50);

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisLockService> _logger;

        public RedisLockService(IConnectionMultiplexer redis, ILogger<RedisLockService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan ttl, TimeSpan waitFor, CancellationToken cancellationToken)
        {
            var db = _redis.GetDatabase();
            var token = Guid.NewGuid().ToString("N");
            var deadline = DateTimeOffset.UtcNow + waitFor;

            while (true)
            {
                if (await db.LockTakeAsync(key, token, ttl))
                {
                    _logger.LogDebug("[RedisLock] acquired key={Key} token={Token} ttl={TtlMs}ms", key, token, (long)ttl.TotalMilliseconds);
                    return new Handle(db, key, token, _logger);
                }
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    _logger.LogWarning("[RedisLock] timeout key={Key} waited={WaitMs}ms", key, (long)waitFor.TotalMilliseconds);
                    return null;
                }
                try
                {
                    await Task.Delay(PollDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private sealed class Handle : IAsyncDisposable
        {
            private readonly IDatabase _db;
            private readonly string _key;
            private readonly string _token;
            private readonly ILogger _logger;
            private int _released;

            public Handle(IDatabase db, string key, string token, ILogger logger)
            {
                _db = db;
                _key = key;
                _token = token;
                _logger = logger;
            }

            public async ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _released, 1) != 0)
                    return;
                try
                {
                    await _db.LockReleaseAsync(_key, _token);
                    _logger.LogDebug("[RedisLock] released key={Key} token={Token}", _key, _token);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[RedisLock] release failed key={Key} token={Token}", _key, _token);
                }
            }
        }
    }
}
