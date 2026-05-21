using ChurrOS.Api.Models.Dtos.Environment;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class Environment
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
        public string Type { get; protected set; }

        [MaxLength(4095)]
        [Required]
        public string[] Host { get; set; }

        [Required]
        public int Port { get; set; }

        [Required]
        public byte[] SshPublicKey { get; set; }

        [MaxLength]
        public string EncryptionKey { get; set; }

        [Required]
        public long AclId { get; protected set; }
        public virtual Acl? Acl { get; protected set; }

        [Required]
        public EnvironmentProvisionStatus ProvisionStatus { get; set; }

        public EnvironmentDefinition? Definition { get; set; }

        public EnvironmentHealthItem? Health { get; set; }

        [Required]
        public string[] Tags { get; set; }

        [Required]
        public JsonElement Metadata { get; protected set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        [Required]
        public long CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; set; }

        [Required]
        public long ModifiedById { get; set; }
        public virtual Identity? ModifiedBy { get; protected set; }

        public Environment(long accountId, long id, string name, string type, string[] host, int port, long aclId, EnvironmentProvisionStatus provisionStatus, byte[] sshPublicKey, string encryptionKey, EnvironmentDefinition? definition, string[] tags, JsonElement metadata, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            Id = id;
            Name = name;
            Type = type;
            Host = host;
            Port = port;
            AclId = aclId;
            ProvisionStatus = provisionStatus;
            SshPublicKey = sshPublicKey;
            EncryptionKey = encryptionKey;
            Definition = definition;
            Tags = tags;
            Metadata = metadata;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
