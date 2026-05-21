using ChurrOS.Api.Models.Dtos.Template;
using Mapster;

namespace ChurrOS.Api.Mappers
{
    public class TeplateMapper : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            TypeAdapterConfig<Domain.TemplateCategory, TemplateCategorySummary>
                .NewConfig();
            TypeAdapterConfig<Domain.Template, TemplateSummary>
                .NewConfig();
            TypeAdapterConfig<Domain.Template, TemplateItem>
                .NewConfig();
        }
    }
}
