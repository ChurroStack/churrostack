using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class StopApplication : IRequest<StopApplication, Task>
    {
        public string Name { get; private set; }
        public string? DeploymentName { get; private set; }

        public StopApplication(string name, string? deploymentName)
        {
            Name = name;
            DeploymentName = deploymentName;
        }
    }
}
