using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeleteApplication : IRequest<DeleteApplication, Task>
    {
        public string Name { get; private set; }
        public string? DeploymentName { get; private set; }

        public DeleteApplication(string name, string? deploymentName = null)
        {
            Name = name;
            DeploymentName = deploymentName;
        }
    }
}
