using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Identity;
using System.Text.Json;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationDeploymentItem
    {
        public string Name { get; protected set; }
        public IdentitySummary Owner { get; set; }
        public byte[] DeploymentHash { get; protected set; }
        public IDictionary<string, double>? Metrics { get; set; }
        public DeploymentProvisionStatus ProvisionStatus { get; protected set; }
        public DeploymentExecutionStatus ExecutionStatus { get; protected set; }
        public DeploymentStatus? DeploymentStatus { get; protected set; }
        public DateTimeOffset? DeployedAt { get; protected set; }
        public JsonElement? Metadata { get; protected set; }
        public string[] Tags { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public ApplicationDeploymentItem(string name, IdentitySummary owner, byte[] deploymentHash, DeploymentProvisionStatus provisionStatus, DeploymentExecutionStatus executionStatus, DeploymentStatus? deploymentStatus, DateTimeOffset? deployedAt, JsonElement? metadata, string[] tags, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, IDictionary<string, double>? metrics)
        {
            Name = name;
            Owner = owner;
            DeploymentHash = deploymentHash;
            ProvisionStatus = provisionStatus;
            ExecutionStatus = executionStatus;
            DeploymentStatus = deploymentStatus;
            DeployedAt = deployedAt;
            Metadata = metadata;
            Tags = tags;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            Metrics = metrics;
        }
    }
}
