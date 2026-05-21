using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Models.Dtos.Deployment
{
    public class DeploymentExtensionRequestItem
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Template { get; set; }

        [Required]
        public IDictionary<string, string[]> Parameters { get; set; }

        public DeploymentExtensionRequestItem(string name, string template, IDictionary<string, string[]> parameters)
        {
            Name = name;
            Template = template;
            Parameters = parameters;
        }
    }
}
