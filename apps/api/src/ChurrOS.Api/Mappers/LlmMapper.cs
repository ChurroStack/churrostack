using ChurrOS.Api.Models.Dtos.Llm;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class LlmMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.Llm, LlmItem>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.ToString())
                .Map(dest => dest.Members, src => src.Acl!.Members);
            TypeAdapterConfig<Domain.Llm, LlmSummary>
                .NewConfig()
                .Map(dest => dest.Id, src => src.Id.ToString());
        }
    }
}
