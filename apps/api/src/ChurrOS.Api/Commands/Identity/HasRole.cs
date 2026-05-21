using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class HasRole : IRequest<HasRole, ValueTask<bool>>
    {
        public IdentityRole Role { get; }

        public long IdentityId { get; }

        public HasRole(IdentityRole role, long identityId)
        {
            Role = role;
            IdentityId = identityId;
        }
    }
}
