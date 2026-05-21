using ChurrOS.Api.Models.Dtos.Environment;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class EnvironmentMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.Environment, EnvironmentItem>
                .NewConfig()
                .Map(dest => dest.Members, src => src.Acl!.Members);
            TypeAdapterConfig<Domain.Environment, EnvironmentSummary>
                .NewConfig();
        }
    }
}
