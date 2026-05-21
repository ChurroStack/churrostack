using ChurrunKubernetes.Models.Dtos.Deployment;

namespace ChurrunKubernetes.Domain
{
    public class Deployment
    {
        public string Name { get; set; }
        public string AppName { get; set; }
        public DeploymentSizeItem? Size { get; private set; }
        public PortDefinition[] Ports { get; set; }

        public Deployment(string name, string appName, DeploymentSizeItem? size, PortDefinition[] ports)
        {
            Name = name;
            AppName = appName;
            Size = size;
            Ports = ports;
        }
    }
}
