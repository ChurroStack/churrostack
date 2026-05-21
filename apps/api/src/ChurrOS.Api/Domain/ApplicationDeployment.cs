using ChurrOS.Api.Models.Dtos.Deployment;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    public class ApplicationDeployment
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long ApplicationId { get; protected set; }
        public virtual Application? Application { get; protected set; }

        [Required]
        public byte[] DeploymentHash { get; set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; protected set; }

        public long? OwnerId { get; protected set; }
        public virtual Identity? Owner { get; protected set; }

        [Required]
        public DeploymentProvisionStatus ProvisionStatus { get; set; }

        [Required]
        public DeploymentExecutionStatus ExecutionStatus { get; set; }

        public DeploymentStatus? DeploymentStatus { get; set; }

        public DateTimeOffset? DeployedAt { get; set; }

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

        public ApplicationDeployment(long accountId, long applicationId, byte[] deploymentHash, string name, long? ownerId, DeploymentProvisionStatus provisionStatus, DeploymentExecutionStatus executionStatus, DeploymentStatus? deploymentStatus, DateTimeOffset? deployedAt, string[] tags, JsonElement? metadata, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            ApplicationId = applicationId;
            DeploymentHash = deploymentHash;
            Name = name;
            OwnerId = ownerId;
            ProvisionStatus = provisionStatus;
            ExecutionStatus = executionStatus;
            DeploymentStatus = deploymentStatus;
            DeployedAt = deployedAt;
            Tags = tags;
            Metadata = metadata;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
