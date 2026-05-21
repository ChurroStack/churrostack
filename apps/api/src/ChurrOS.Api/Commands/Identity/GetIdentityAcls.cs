using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityAcls : IRequest<GetIdentityAcls, ValueTask<IDictionary<long, Permission>>>
    {
        public long IdentityId { get; protected set; }
        public Permission Permission { get; protected set; }

        public GetIdentityAcls(long identityId, Permission? permission = null)
        {
            IdentityId = identityId;
            Permission = permission ?? Permission.Read;
        }
    }
}
