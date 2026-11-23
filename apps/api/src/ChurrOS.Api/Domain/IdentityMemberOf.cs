using ChurrOS.Api.Models.Dtos.Identity;
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

        /// <summary>Local = added by an admin (never removed by sync); Entra = added by the Entra sync.</summary>
        public IdentitySource Source { get; protected set; }

        public IdentityMemberOf(long accountId, long identityId, long groupId, IdentitySource source = IdentitySource.Local)
        {
            Source = source;
            AccountId = accountId;
            IdentityId = identityId;
            GroupId = groupId;
        }
    }
}
