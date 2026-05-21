using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Identity
{
    public class EnsureHasRole : IRequest<EnsureHasRole, Task>
    {
        public long? IdentityId { get; }

        public IdentityRole Role { get; }

        public EnsureHasRole(IdentityRole role, long? identityId = null)
        {
            IdentityId = identityId;
            Role = role;
        }
    }
}
