using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Models.Dtos.Deployment
{
    public class DeploymentRequestItem
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string AppName { get; set; }

        [Required]
        public string Template { get; set; }

        [Required]
        public int Replicas { get; set; }

        [Required]
        public SizeRequestItem Size { get; set; }

        [Required]
        public ApplicationEnvironmentVariable[] Variables { get; set; }

        [Required]
        public IDictionary<string, string[]> Parameters { get; set; }

        [Required]
        public PortDefinition[] Ports { get; set; }

        [Required]
        public DeploymentExtensionRequestItem[] Extensions { get; set; }

        public DeploymentRequestItem(string name, string appName, string template, int replicas, SizeRequestItem size, IDictionary<string, string[]> parameters, DeploymentExtensionRequestItem[] extensions, PortDefinition[] ports, ApplicationEnvironmentVariable[] variables)
        {
            Name = name;
            AppName = appName;
            Template = template;
            Size = size;
            Parameters = parameters;
            Extensions = extensions;
            Ports = ports;
            Variables = variables;
        }
    }
}
