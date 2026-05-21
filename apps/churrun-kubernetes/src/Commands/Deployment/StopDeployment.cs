using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class StopDeployment : IRequest<StopDeployment, Task>
    {
        public string Name { get; private set; }
        public byte[]? Hash { get; private set; }

        public StopDeployment(string name, byte[]? hash = null)
        {
            Name = name;
            Hash = hash;
        }
    }
}
