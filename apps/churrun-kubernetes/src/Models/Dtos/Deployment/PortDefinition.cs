namespace ChurrunKubernetes.Models.Dtos.Deployment
{
    public class PortDefinition
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public IList<IDictionary<string, string>>? Transforms { get; set; }

        public PortDefinition(string name, int port, IList<IDictionary<string, string>>? transforms)
        {
            Name = name;
            Port = port;
            Transforms = transforms;
        }
    }
}
