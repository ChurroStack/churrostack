using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class DeleteIdentity : IRequest<DeleteIdentity, Task>
    {
        public string IdentityName { get; protected set; }

        public DeleteIdentity(string identityName)
        {
            IdentityName = identityName;
        }
    }
}
