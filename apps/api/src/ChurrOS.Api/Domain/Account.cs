using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    public class Account
    {
        [Required]
        public long Id { get; protected set; }

        [Required]
        public string Name { get; protected set; }

        [Required]
        public string[] Domains { get; protected set; }

        [Required]
        public string[] Owners { get; protected set; } = Array.Empty<string>();

        [MaxLength]
        public string EncryptionKey { get; protected set; } = string.Empty;

        [Required]
        public IDictionary<string, object>? Metadata { get; protected set; }

        public Account(long id, string name, string[] domains, string[] owners, string encryptionKey, IDictionary<string, object>? metadata)
        {
            Id = id;
            Owners = owners;
            EncryptionKey = encryptionKey;
            Name = name;
            Domains = domains ?? [];
            Metadata = metadata;
        }

        public void SetOwners(string[] owners)
        {
            Owners = owners;
        }

        public void SetContent(IDictionary<string, object>? content)
        {
            Metadata = content;
        }
    }
}
