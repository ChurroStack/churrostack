using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    /// <summary>
    /// Lists every application of an environment with its usage statistics and size
    /// recommendation (used by the environment Usage tab).
    /// </summary>
    public class GetEnvironmentUsage : IRequest<GetEnvironmentUsage, ValueTask<IList<EnvironmentUsageItem>>>
    {
        public string EnvironmentName { get; private set; }

        public GetEnvironmentUsage(string environmentName)
        {
            EnvironmentName = environmentName;
        }
    }
}
