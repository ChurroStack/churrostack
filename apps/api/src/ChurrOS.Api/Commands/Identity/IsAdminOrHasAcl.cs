using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class IsAdminOrHasAcl : IRequest<IsAdminOrHasAcl, ValueTask<bool>>
    {
        public long AclId { get; }
        public Permission Permission { get; }

        public IsAdminOrHasAcl(long aclId, Permission permission)
        {
            AclId = aclId;
            Permission = permission;
        }
    }
}
