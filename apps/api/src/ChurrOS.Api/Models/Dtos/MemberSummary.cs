using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos
{
    public class MemberSummary
    {
        public IdentitySummary Identity { get; private set; }
        public Permission Permission { get; private set; }

        public MemberSummary(IdentitySummary identity, Permission permission)
        {
            Identity = identity;
            Permission = permission;
        }
    }
}
