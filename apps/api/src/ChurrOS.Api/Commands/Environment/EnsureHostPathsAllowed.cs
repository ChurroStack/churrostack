using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if any requested storage host
    /// path is not one the current user is allowed to use in the environment. Shared
    /// by application create and update so the user-scoped gate is enforced on both.
    /// </summary>
    public class EnsureHostPathsAllowed : IRequest<EnsureHostPathsAllowed, ValueTask<bool>>
    {
        public string EnvironmentName { get; protected set; }
        public IReadOnlyCollection<string> RequestedHostPaths { get; protected set; }

        public EnsureHostPathsAllowed(string environmentName, IReadOnlyCollection<string> requestedHostPaths)
        {
            EnvironmentName = environmentName;
            RequestedHostPaths = requestedHostPaths;
        }
    }
}
