using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class Application
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long EnvironmentId { get; protected set; }
        public virtual Environment? Environment { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        [Required]
        public long AclId { get; protected set; }
        public virtual Acl? Acl { get; protected set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; protected set; }

        [Required]
        public long TemplateId { get; protected set; }
        public virtual Template? Template { get; protected set; }

        [Required]
        public ApplicationMode Mode { get; protected set; }

        [Required]
        public SizeRequestItem Size { get; set; }

        [Required]
        public int Replicas { get; protected set; }

        [Required]
        public IDictionary<string, string[]> Parameters { get; set; }

        [Required]
        public ApplicationEnvironmentVariable[] Variables { get; set; }

        public virtual ICollection<ApplicationDeployment>? Deployments { get; protected set; }
        public virtual ICollection<ApplicationExtension>? Extensions { get; protected set; }

        public PortDefinition[]? Ports { get; set; }

        [Required]
        public byte[] DeploymentHash { get; set; }

        [Required]
        public string[] Tags { get; set; }

        public JsonElement? Metadata { get; set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        [Required]
        public long CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; set; }

        [Required]
        public long ModifiedById { get; set; }
        public virtual Identity? ModifiedBy { get; protected set; }

        public Application(long accountId, long environmentId, long id, long aclId, string name, long templateId, ApplicationMode mode, SizeRequestItem size, int replicas, IDictionary<string, string[]> parameters, ApplicationEnvironmentVariable[] variables, PortDefinition[]? ports, byte[] deploymentHash, string[] tags, JsonElement? metadata, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            EnvironmentId = environmentId;
            Id = id;
            AclId = aclId;
            Name = name;
            TemplateId = templateId;
            Mode = mode;
            Size = size;
            Replicas = replicas;
            Parameters = parameters;
            Variables = variables;
            Ports = ports;
            DeploymentHash = deploymentHash;
            Tags = tags;
            Metadata = metadata;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
