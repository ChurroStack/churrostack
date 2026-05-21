using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentSummary
    {
        public string Name { get; private set; }
        public string Uri { get; private set; }
        public EnvironmentProvisionStatus ProvisionStatus { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public IdentitySummary CreatedBy { get; private set; }
        public DateTimeOffset ModifiedAt { get; private set; }
        public IdentitySummary ModifiedBy { get; private set; }

        public EnvironmentSummary(string name, string uri, EnvironmentProvisionStatus provisionStatus, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy)
        {
            Name = name;
            Uri = uri;
            ProvisionStatus = provisionStatus;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
        }
    }
}
