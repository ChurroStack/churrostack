namespace ChurrOS.Api.Models.Dtos.Deployment.Remote
{
    public class DeploymentBody
    {
        public string Name { get; private set; }

        public string Template { get; internal set; }

        public int? Replicas { get; private set; }

        public DeploymentSizeItem? Size { get; private set; }

        public IDictionary<string, string>? Parameters { get; private set; }

        public ExtensionBody[]? Extensions { get; private set; }

        public DeploymentBody(string name, string template, int? replicas, DeploymentSizeItem? size, IDictionary<string, string>? parameters, ExtensionBody[]? extensions)
        {
            Name = name;
            Template = template;
            Replicas = replicas;
            Size = size;
            Parameters = parameters;
            Extensions = extensions;
        }
    }
}
