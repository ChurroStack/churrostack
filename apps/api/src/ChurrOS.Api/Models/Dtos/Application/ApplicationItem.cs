using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using System.Text.Json;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationItem
    {
        public string Name { get; protected set; }
        public MemberSummary[] Members { get; protected set; }
        public string EnvironmentName { get; protected set; }
        public TemplateItem Template { get; protected set; }
        public ApplicationMode Mode { get; protected set; }
        public SizeRequestItem Size { get; protected set; }
        public int Replicas { get; protected set; }
        public ApplicationEnvironmentVariable[] Variables { get; protected set; }
        public IDictionary<string, string[]> Parameters { get; protected set; }
        public virtual ICollection<ApplicationExtensionItem>? Extensions { get; protected set; }
        public virtual ICollection<ApplicationDeploymentItem>? Deployments { get; protected set; }
        public byte[] DeploymentHash { get; protected set; }
        public PortDefinitionItem[]? Ports { get; set; }
        public JsonElement? Metadata { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }
        public string[] Tags { get; protected set; }

        public ApplicationItem(string name, MemberSummary[] members, string environmentName, TemplateItem template, ApplicationMode mode, SizeRequestItem size, int replicas, ApplicationEnvironmentVariable[] variables, IDictionary<string, string[]> parameters, ICollection<ApplicationExtensionItem>? extensions, ICollection<ApplicationDeploymentItem>? deployments, byte[] deploymentHash, PortDefinitionItem[]? ports, JsonElement? metadata, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, string[]? tags = null)
        {
            Name = name;
            Members = members;
            EnvironmentName = environmentName;
            Template = template;
            Mode = mode;
            Size = size;
            Replicas = replicas;
            Variables = variables;
            Parameters = parameters;
            Extensions = extensions;
            Deployments = deployments;
            DeploymentHash = deploymentHash;
            Ports = ports;
            Metadata = metadata;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            Tags = tags ?? Array.Empty<string>();
        }
    }
}
