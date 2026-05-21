using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class ApiKey
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        [Required]
        public string Description { get; protected set; }

        [Required]
        public byte[] Value { get; protected set; }

        [Required]
        public DateTimeOffset ExpiresAt { get; protected set; }

        [Required]
        public long IdentityId { get; protected set; }
        public virtual Identity? Identity { get; protected set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        [Required]
        public long CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; protected set; }

        [Required]
        public long ModifiedById { get; protected set; }
        public virtual Identity? ModifiedBy { get; protected set; }

        public ApiKey(long accountId, long id, string description, byte[] value, DateTimeOffset expiresAt, long identityId, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            Id = id;
            Description = description;
            Value = value;
            ExpiresAt = expiresAt;
            IdentityId = identityId;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedById = modifiedById;
            ModifiedAt = modifiedAt;
        }
    }
}
