using ChurrOS.Api.Models.Dtos;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class AclMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.AclMember, MemberItem>
                .NewConfig()
                .Map(dest => dest.IdentityName, src => src.Identity!.Name);
            TypeAdapterConfig<Domain.AclMember, MemberSummary>
                .NewConfig();
        }
    }
}
