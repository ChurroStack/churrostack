using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Identity;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationSummary
    {
        public string Name { get; protected set; }
        public ApplicationMode Mode { get; protected set; }
        public string TemplateName { get; protected set; }
        public string? EnvironmentName { get; set; }
        public DeploymentProvisionStatus ProvisionStatus { get; set; }
        public DeploymentExecutionStatus ExecutionStatus { get; set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }
        public IDictionary<string, double>? Metrics { get; set; }

        public ApplicationSummary() : this(null!, ApplicationMode.Application, null!, null, default, null!, default, null!, null) { }

        [JsonConstructor]
        public ApplicationSummary(string name, ApplicationMode mode, string templateName, string? environmentName, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, IDictionary<string, double>? metrics)
        {
            Name = name;
            Mode = mode;
            TemplateName = templateName;
            EnvironmentName = environmentName;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            Metrics = metrics;
        }
    }
}
