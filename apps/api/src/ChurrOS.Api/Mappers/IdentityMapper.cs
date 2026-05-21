using ChurrOS.Api.Models.Dtos.Identity;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class IdentityMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.Identity, IdentityItem>
                .NewConfig();
            TypeAdapterConfig<Domain.Identity, IdentitySummary>
                .NewConfig();
        }
    }
}
