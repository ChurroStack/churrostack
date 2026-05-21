using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class IdentityItem
    {
        public long Id { get; private set; }

        public string Name { get; private set; }

        public string DisplayName { get; private set; }

        public IdentityRole Role { get; private set; }

        public IdentityType Type { get; private set; }

        public DateTimeOffset ModifiedAt { get; private set; }

        [JsonConstructor]
        public IdentityItem(long id, string name, string displayName, IdentityRole role, IdentityType type, DateTimeOffset modifiedAt)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            Role = role;
            Type = type;
            ModifiedAt = modifiedAt;
        }
    }
}
