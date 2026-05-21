namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class ProfileItem
    {
        public string Name { get; protected set; }
        public string DisplayName { get; protected set; }
        public string AccountName { get; protected set; }
        public IdentityRole Role { get; protected set; }
        public string[] MemberOf { get; private set; }
        public string Language { get; private set; }
        public string? Location { get; private set; }
        public string? Timezone { get; private set; }
        public bool CanCreateApplications { get; set; }
        public IDictionary<string, string> Claims { get; protected set; }
        public IDictionary<string, object> Metadata { get; protected set; }

        public ProfileItem(string name, string displayName, string accountName, IdentityRole role, string[] memberOf, bool canCreateApplications, IDictionary<string, string>? claims = null, IDictionary<string, object>? metadata = null)
        {
            Name = name;
            DisplayName = displayName;
            AccountName = accountName;
            Role = role;
            MemberOf = memberOf;
            CanCreateApplications = canCreateApplications;
            Metadata = metadata ?? new Dictionary<string, object>();
            if (claims?.TryGetValue("language", out var metadataLanguage) ?? false)
            {
                Language = metadataLanguage?.ToString() ?? "en";
            }
            else
            {
                Language = "en";
            }
            if (claims?.TryGetValue("location", out var metadataLocation) ?? false)
            {
                Location = metadataLocation?.ToString();
            }
            else
            {
                Location = null;
            }
            if (claims?.TryGetValue("timezone", out var metadataTimezone) ?? false)
            {
                Timezone = metadataTimezone?.ToString();
            }
            else
            {
                Timezone = null;
            }

            var ignoredClaims = new string[] { "location", "timezone", "language" };
            Claims = claims?.Where(o => !ignoredClaims.Contains(o.Key.ToLowerInvariant())).ToDictionary(o => o.Key, o => o.Value) ?? [];
        }
    }
}
