namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class IdentitySummary
    {
        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public IdentityType Type { get; private set; }
        public IdentityRole Role { get; private set; }

        public IdentitySummary(string name, string displayName, IdentityType type, IdentityRole role)
        {
            Name = name;
            DisplayName = displayName;
            Type = type;
            Role = role;
        }
    }
}
