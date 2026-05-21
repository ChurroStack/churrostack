using ChurrOS.Api.Models.Dtos.Identity;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    public class Identity
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; protected set; }

        [MaxLength(255)]
        [Required]
        public string DisplayName { get; protected set; }

        [DefaultValue(IdentityRole.User)]
        [Required]
        public IdentityRole Role { get; protected set; }

        [Required]
        public IdentityType Type { get; protected set; }

        [Required]
        public IdentityProperties Properties { get; protected set; }

        public virtual ICollection<IdentityMemberOf> MemberOf { get; protected set; }

        public long? AclId { get; protected set; }
        public virtual Acl? Acl { get; protected set; }

        public DateTimeOffset? LockAfter { get; protected set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        public long? CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; set; }

        public long? ModifiedById { get; set; }
        public virtual Identity? ModifiedBy { get; protected set; }

        public Identity(long accountId, long id, string name, string displayName, IdentityType type, IdentityRole role, DateTimeOffset createdAt, long? createdById, DateTimeOffset modifiedAt, long? modifiedById, long? aclId = null, IdentityProperties? properties = default, DateTimeOffset? lockAfter = null)
        {
            MemberOf = new List<IdentityMemberOf>();
            AccountId = accountId;
            Id = id;
            Name = name.ToLowerInvariant().Trim();
            DisplayName = displayName;
            Type = type;
            Role = role;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
            AclId = aclId;
            Properties = properties ?? new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            LockAfter = lockAfter;
        }

        public void SetDisplayName(string displayName)
        {
            DisplayName = displayName;
        }

        public void SetRole(IdentityRole role)
        {
            Role = role;
        }

        public void SetModified(long identityId, DateTimeOffset date)
        {
            ModifiedById = identityId;
            ModifiedAt = date;
        }

        public void SetMemberOf(IEnumerable<IdentityMemberOf> groups)
        {
            MemberOf = (ICollection<IdentityMemberOf>)groups;
        }

        public void SetLanguage(string language)
        {
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Claims == null)
                Properties.Claims = new Dictionary<string, string>();
            if (!Properties.Claims.TryAdd("language", language))
                Properties.Claims["language"] = language;

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void SetCompany(string company)
        {
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Claims == null)
                Properties.Claims = new Dictionary<string, string>();
            if (!Properties.Claims.TryAdd("company", company))
                Properties.Claims["company"] = company;

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void SetLocation(string? location)
        {
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Claims == null)
                Properties.Claims = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(location))
            {
                if (Properties.Claims.ContainsKey("location"))
                    Properties.Claims.Remove("location");
            }
            else
            {
                if (!Properties.Claims.TryAdd("location", location))
                    Properties.Claims["location"] = location;
            }

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void SetTimezone(string? timezone)
        {
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Claims == null)
                Properties.Claims = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(timezone))
            {
                if (Properties.Claims.ContainsKey("timezone"))
                    Properties.Claims.Remove("timezone");
            }
            else
            {
                if (!Properties.Claims.TryAdd("timezone", timezone))
                    Properties.Claims["timezone"] = timezone;
            }

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void SetAutoSaveByDefault(bool? value)
        {
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Claims == null)
                Properties.Claims = new Dictionary<string, string>();
            if (!value.HasValue)
            {
                if (Properties.Claims.ContainsKey("autoSaveByDefault"))
                    Properties.Claims.Remove("autoSaveByDefault");
            }
            else
            {
                if (!Properties.Claims.TryAdd("autoSaveByDefault", value.Value.ToString()))
                    Properties.Claims["autoSaveByDefault"] = value.Value.ToString();
            }

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void SetToken(string serviceId, string loginHint, string scope, string accessToken, string refreshToken, DateTimeOffset expiresAt)
        {
            var key = $"{serviceId.Trim()}|{loginHint.Trim()}|{scope.Trim()}".Trim().ToLowerInvariant();
            if (Properties == null)
                Properties = new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            if (Properties.Tokens == null)
                Properties.Tokens = new Dictionary<string, IdentityToken>();
            var token = new IdentityToken(loginHint, accessToken, refreshToken, scope, expiresAt);
            if (Properties.Tokens.ContainsKey(key))
            {
                Properties.Tokens[key] = token;
            }
            else
            {
                Properties.Tokens.Add(key, token);
            }
            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }

        public void RemoveAccessToken(string serviceId, string loginHint, string scope)
        {
            var key = $"{serviceId.Trim()}|{loginHint.Trim()}|{scope.Trim()}".Trim().ToLowerInvariant();
            if (Properties.Tokens?.ContainsKey(key) ?? false)
            {
                Properties.Tokens?.Remove(key);
            }
            Properties = new IdentityProperties(Properties?.Claims ?? new Dictionary<string, string>(), Properties?.Tokens ?? new Dictionary<string, IdentityToken>(), Properties?.Metadata ?? new Dictionary<string, object>());
        }

        public void UpsertMetadata(IDictionary<string, object> metadata)
        {
            Properties ??= new IdentityProperties(new Dictionary<string, string>(), new Dictionary<string, IdentityToken>(), new Dictionary<string, object>());
            Properties.Metadata ??= new Dictionary<string, object>();

            foreach (var item in metadata)
            {
                if (Properties.Metadata.ContainsKey(item.Key))
                {
                    Properties.Metadata[item.Key] = item.Value;
                }
                else
                {
                    Properties.Metadata.Add(item.Key, item.Value);
                }
            }

            //TODO: remove items ??

            Properties = new IdentityProperties(Properties.Claims, Properties.Tokens, Properties.Metadata);
        }
    }
}
