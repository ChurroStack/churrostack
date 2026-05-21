using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityGroupsIds : IRequest<GetIdentityGroupsIds, ValueTask<long[]>>
    {
        public long IdentityId { get; protected set; }
        public bool IncludeSelf { get; protected set; }

        public GetIdentityGroupsIds(long identityId, bool includeSelf = false)
        {
            IdentityId = identityId;
            IncludeSelf = includeSelf;
        }
    }
}
