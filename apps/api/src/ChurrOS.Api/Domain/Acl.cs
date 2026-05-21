using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class Acl
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        public virtual ICollection<AclMember>? Members { get; protected set; }

        public Acl(long accountId, long id)
        {
            AccountId = accountId;
            Id = id;
        }
    }
}
