using ChurrOS.Api.Models.Dtos.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(AclId), nameof(IdentityId))]
    public class AclMember
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long AclId { get; protected set; }
        public virtual Acl? Acl { get; protected set; }

        [Required]
        public long IdentityId { get; protected set; }
        public virtual Identity? Identity { get; protected set; }

        [Required]
        public Permission Permission { get; set; }

        public AclMember(long accountId, long aclId, long identityId, Permission permission)
        {
            AccountId = accountId;
            AclId = aclId;
            IdentityId = identityId;
            Permission = permission;
        }
    }
}
