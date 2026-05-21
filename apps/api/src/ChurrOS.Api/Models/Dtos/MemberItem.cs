using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos
{
    public class MemberItem
    {
        public string IdentityName { get; private set; }
        public Permission Permission { get; private set; }

        public MemberItem(string identityName, Permission permission)
        {
            IdentityName = identityName;
            Permission = permission;
        }
    }
}
