using ChurrunKubernetes.Models.Dtos.Deployment;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Environment
{
    public class EnsureEnvironmentHasQuota : IRequest<EnsureEnvironmentHasQuota, Task>
    {
        public DeploymentSizeItem? Size { get; protected set; }

        public EnsureEnvironmentHasQuota(DeploymentSizeItem? size)
        {
            Size = size;
        }
    }
}
