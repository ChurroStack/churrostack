using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class IdentityWithAssignedItem : IdentityItem
    {
        public string[] Assigned { get; private set; } = Array.Empty<string>();

        public string? ClientSecret { get; set; }

        [JsonConstructor]
        public IdentityWithAssignedItem(long id, string name, string displayName, IdentityRole role, IdentityType type, string[] assigned, DateTimeOffset modifiedAt, string? clientSecret = null) : base(id, name, displayName, role, type, modifiedAt)
        {
            Assigned = assigned;
            ClientSecret = clientSecret;
        }
    }
}
