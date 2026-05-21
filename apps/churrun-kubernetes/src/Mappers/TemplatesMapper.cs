using ChurrunKubernetes.Models.Dtos.Template;
using Mapster;

namespace ChurrunKubernetes.Mappers
{
    public class TemplatesMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.Template, TemplateSummary>
                .NewConfig();
        }
    }
}
