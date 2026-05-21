using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Deployment
{
    public class DeleteDeployment : IRequest<DeleteDeployment, Task>
    {
        public string Name { get; private set; }
        public byte[]? Hash { get; private set; }

        public DeleteDeployment(string name, byte[]? hash = null)
        {
            Name = name;
            Hash = hash;
        }
    }
}
