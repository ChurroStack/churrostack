using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class StartDeployment : IRequest<StartDeployment, Task>
    {
        public string Name { get; private set; }
        public int Replicas { get; private set; }
        public byte[]? Hash { get; private set; }

        public StartDeployment(string name, int replicas = 1, byte[]? hash = null)
        {
            Name = name;
            Replicas = replicas;
            Hash = hash;
        }
    }
}
