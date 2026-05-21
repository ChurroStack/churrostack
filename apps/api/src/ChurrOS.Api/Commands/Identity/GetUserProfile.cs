using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;
using System.Security.Claims;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetUserProfile : IRequest<GetUserProfile, ValueTask<ProfileItem>>
    {
        public string IdentityName { get; private set; }
        public Claim[] Claims { get; private set; }

        public GetUserProfile(string identityName, Claim[] claims)
        {
            IdentityName = identityName;
            Claims = claims;
        }
    }
}
