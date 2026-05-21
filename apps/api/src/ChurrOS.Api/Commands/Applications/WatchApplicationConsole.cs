using DispatchR.Abstractions.Stream;

namespace ChurrOS.Api.Commands.Applications
{
    public class WatchApplicationConsole : IStreamRequest<WatchApplicationConsole, string>
    {
        public string Name { get; private set; }
        public string DeploymentName { get; private set; }

        public WatchApplicationConsole(string name, string deploymentName)
        {
            Name = name;
            DeploymentName = deploymentName;
        }
    }
}
