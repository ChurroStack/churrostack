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

        /// <summary>Who owns the identity: an admin (local) or the Microsoft Entra sync (entra).</summary>
        public IdentitySource Source { get; private set; }

        /// <summary>True when a local identity has been matched to an Entra object (display name/memberships are kept in sync, but it is never disabled).</summary>
        public bool Linked { get; private set; }

        /// <summary>True when access is blocked (LockAfter reached), e.g. the user was removed from Entra.</summary>
        public bool Disabled { get; private set; }

        [JsonConstructor]
        public IdentityItem(long id, string name, string displayName, IdentityRole role, IdentityType type, DateTimeOffset modifiedAt, IdentitySource source = IdentitySource.Local, bool linked = false, bool disabled = false)
        {
            Source = source;
            Linked = linked;
            Disabled = disabled;
            Id = id;
            Name = name;
            DisplayName = displayName;
            Role = role;
            Type = type;
            ModifiedAt = modifiedAt;
        }
    }
}
