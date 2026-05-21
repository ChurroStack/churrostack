using ChurrOS.Api.Models.Dtos.ApiKey;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class ApiKeyMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.ApiKey, ApiKeyItem>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.ToString());
        }
    }
}
