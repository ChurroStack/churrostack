using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Template
{
    public class CreateTemplate : IRequest<CreateTemplate, ValueTask<TemplateSummary>>
    {
        public string Template { get; set; }

        public CreateTemplate(string template)
        {
            Template = template;
        }
    }
}
