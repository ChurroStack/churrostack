using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    public class IdentityMemberOf
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long IdentityId { get; protected set; }
        public virtual Identity? Identity { get; protected set; }

        [Required]
        public long GroupId { get; protected set; }
        public virtual Identity? Group { get; protected set; }

        public IdentityMemberOf(long accountId, long identityId, long groupId)
        {
            AccountId = accountId;
            IdentityId = identityId;
            GroupId = groupId;
        }
    }
}
