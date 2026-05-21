using ChurrOS.Api.Utils;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace ChurrOS.Api.Services.Redis
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly IConnectionMultiplexer _redis;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

        internal class CacheServiceContext : ICacheServiceContext
        {
            public TimeSpan AbsoluteExpiration { get; private set; }

            public CacheServiceContext()
            {
                AbsoluteExpiration = TimeSpan.FromMinutes(5);
            }

            public void SetAbsoluteExpiration(TimeSpan expiration)
            {
                AbsoluteExpiration = expiration;
            }
        }

        public RedisCacheService(IDistributedCache cache, IConnectionMultiplexer redis)
        {
            _cache = cache;
            _redis = redis;
        }

        public T GetOrAdd<T>(string key, Func<ICacheServiceContext, T> getOrAdd)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            try
            {
                semaphore.Wait();
                var context = new CacheServiceContext();

                T? result = default;
                var jsonData = _cache.GetString(key);
                if (jsonData == null)
                {
                    result = getOrAdd(context);
                    jsonData = JsonSerializer.Serialize(result, JsonSettings.Value);
                    var options = new DistributedCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = context.AbsoluteExpiration
                    };
                    _cache.SetString(key, jsonData, options);
                }
                else
                {
                    result = JsonSerializer.Deserialize<T>(jsonData, JsonSettings.Value)!;
                }

                return result;
            }
            finally
            {
                semaphore.Release();
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        public async Task SaveAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            try
            {
                await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonSettings.Value), new DistributedCacheEntryOptions()
                {
                    AbsoluteExpirationRelativeToNow = expiration
                });
            }
            finally
            {
                semaphore.Release();
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        public async Task<T> GetOrAddAsync<T>(string key, Func<ICacheServiceContext, Task<T>> getOrAdd, CancellationToken cancellationToken)
        {
            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
            try
            {
                var context = new CacheServiceContext();

                T? result = default;
                var jsonData = await _cache.GetStringAsync(key, cancellationToken);
                if (jsonData == null)
                {
                    result = await getOrAdd(context);
                    jsonData = JsonSerializer.Serialize(result, JsonSettings.Value);
                    var options = new DistributedCacheEntryOptions()
                    {
                        AbsoluteExpirationRelativeToNow = context.AbsoluteExpiration
                    };
                    await _cache.SetStringAsync(key, jsonData, options);
                }
                else
                {
                    result = JsonSerializer.Deserialize<T>(jsonData, JsonSettings.Value)!;
                }

                return result;
            }
            finally
            {
                semaphore.Release();
                if (semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }

        public void Invalidate(string key)
        {
            _cache.Remove(key);
        }

        public async Task InvalidateAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            var jsonData = await _cache.GetStringAsync(key, cancellationToken);
            if (jsonData == null)
            {
                return default;
            }
            else
            {
                return JsonSerializer.Deserialize<T>(jsonData, JsonSettings.Value)!;
            }

        }

        public async Task InvalidatePrefixAsync(string prefix)
        {
            try
            {
                var field = _cache.GetType()!.BaseType!.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
                var options = field?.GetValue(_cache); // as RedisCacheOptions;
                var systemPrefix = options!.GetType().GetProperty("InstanceName")!.GetValue(options);
                prefix = $"{systemPrefix}{prefix}";
            }
            catch
            {

            }

            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();

            var keys = server.Keys(pattern: $"{prefix}*").ToArray();

            foreach (var key in keys)
            {
                await db.KeyDeleteAsync(key);
            }
        }

        public void InvalidatePrefix(string key)
        {
            InvalidatePrefixAsync(key).GetAwaiter().GetResult();
        }

        public Task<string[]> GetKeysByPrefixAsync(string prefix, CancellationToken cancellationToken)
        {
            string? systemPrefix = null;
            try
            {
                var field = _cache.GetType()!.BaseType!.GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
                var options = field?.GetValue(_cache); // as RedisCacheOptions;
                systemPrefix = (string?)options!.GetType().GetProperty("InstanceName")!.GetValue(options);
                prefix = $"{systemPrefix}{prefix}";
            }
            catch
            {

            }
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();
            var keys = server.Keys(pattern: $"{prefix}*").ToArray();
            return Task.FromResult(keys?.Select(o => systemPrefix != null ? ((string)o!).Substring(systemPrefix.Length) : (string)o!)?.ToArray() ?? [])!;
        }
    }
}
