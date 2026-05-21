using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.ApiKey
{
    public class ApiKeyItem
    {
        public string Id { get; protected set; }
        public string Description { get; protected set; }
        public IdentitySummary Identity { get; protected set; }
        public DateTimeOffset ExpiresAt { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public ApiKeyItem(string id, string description, IdentitySummary identity, DateTimeOffset expiresAt, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy)
        {
            Id = id;
            Description = description;
            Identity = identity;
            ExpiresAt = expiresAt;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
        }
    }
}
