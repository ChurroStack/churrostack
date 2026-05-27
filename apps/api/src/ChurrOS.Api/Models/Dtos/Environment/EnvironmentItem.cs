using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Environment
{
    public class EnvironmentItem
    {
        public string Name { get; private set; }
        public MemberSummary[] Members { get; set; }
        public EnvironmentProvisionStatus ProvisionStatus { get; private set; }
        public EnvironmentDefinition? Definition { get; protected set; }
        public EnvironmentHealthItem? Health { get; protected set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public IdentitySummary CreatedBy { get; private set; }
        public DateTimeOffset ModifiedAt { get; private set; }
        public IdentitySummary ModifiedBy { get; private set; }
        public string[] Tags { get; private set; }

        public EnvironmentItem(string name, MemberSummary[] members, EnvironmentProvisionStatus provisionStatus, EnvironmentHealthItem? health, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, EnvironmentDefinition? definition, string[]? tags = null)
        {
            Name = name;
            Members = members;
            ProvisionStatus = provisionStatus;
            Health = health;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            Definition = definition;
            Tags = tags ?? Array.Empty<string>();
        }
    }
}
