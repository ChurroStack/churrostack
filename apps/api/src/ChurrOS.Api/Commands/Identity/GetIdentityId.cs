using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentityId : IRequest<GetIdentityId, ValueTask<long>>
    {
        public string Name { get; private set; }

        public GetIdentityId(string name)
        {
            Name = name;
        }
    }
}
