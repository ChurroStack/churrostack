namespace ChurrOS.Api.Utils.Exceptions
{
    /// <summary>
    /// Raised when an environment cannot admit a Start/Deploy because its (overcommit-adjusted)
    /// CPU/Memory budget is exhausted, or its start lock is busy. Derives from
    /// <see cref="InvalidOperationException"/> so the existing control-plane HTTP mapping is
    /// preserved, but is a distinct type so callers (e.g. the auto-start activator) can safely
    /// surface its message to end users without leaking arbitrary internal exception text.
    /// </summary>
    public sealed class EnvironmentCapacityException : InvalidOperationException
    {
        public EnvironmentCapacityException(string message) : base(message)
        {
        }
    }
}
