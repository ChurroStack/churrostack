namespace ChurrOS.Api.Services
{
    public interface ICacheServiceContext
    {
        public void SetAbsoluteExpiration(TimeSpan expiration);
    }
    public interface ICacheService
    {
        Task<string[]> GetKeysByPrefixAsync(string key, CancellationToken cancellationToken);
        Task<T> GetOrAddAsync<T>(string key, Func<ICacheServiceContext, Task<T>> getOrAdd, CancellationToken cancellationToken);
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);
        Task SaveAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken);
        T GetOrAdd<T>(string key, Func<ICacheServiceContext, T> getOrAdd);
        Task InvalidateAsync(string key);
        void Invalidate(string key);
        Task InvalidatePrefixAsync(string key);
        void InvalidatePrefix(string key);
    }
}
