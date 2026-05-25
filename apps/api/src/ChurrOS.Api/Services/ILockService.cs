namespace ChurrOS.Api.Services
{
    public interface ILockService
    {
        /// <summary>
        /// Acquires a distributed lock identified by <paramref name="key"/>. Returns a handle
        /// whose disposal releases the lock, or <c>null</c> if the lock could not be acquired
        /// within <paramref name="waitFor"/>.
        /// </summary>
        Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan ttl, TimeSpan waitFor, CancellationToken cancellationToken);
    }
}
