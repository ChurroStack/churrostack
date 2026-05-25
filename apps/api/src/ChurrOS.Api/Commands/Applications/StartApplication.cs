using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class StartApplication : IRequest<StartApplication, Task>
    {
        public string Name { get; private set; }
        public string? DeploymentName { get; private set; }

        public bool BypassAcl { get; init; }

        public StartApplication(string name, string? deploymentName = null)
        {
            Name = name;
            DeploymentName = deploymentName;
        }
    }
}
