using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    /// <summary>
    /// Returns the subset of an environment's configured host paths the current user
    /// is allowed to use (by identity name or group membership). Used by the "Map to"
    /// dropdown and to validate storage-extension saves.
    /// </summary>
    public class GetAllowedHostPaths : IRequest<GetAllowedHostPaths, ValueTask<AllowedHostPathItem[]>>
    {
        public string EnvironmentName { get; protected set; }

        public GetAllowedHostPaths(string environmentName)
        {
            EnvironmentName = environmentName;
        }
    }
}
