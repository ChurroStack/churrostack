using ChurrOS.Api.Models.Dtos.Deployment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeployApplication : IRequest<DeployApplication, ValueTask<DeploymentSummary[]>>
    {
        public string Name { get; private set; }
        public string? DeploymentName { get; private set; }
        public long? DeploymentOwnerId { get; private set; }

        public DeployApplication(string name, string? deploymentName = null, long? deploymentOwnerId = null)
        {
            Name = name;
            DeploymentName = deploymentName;
            DeploymentOwnerId = deploymentOwnerId;
        }
    }
}
