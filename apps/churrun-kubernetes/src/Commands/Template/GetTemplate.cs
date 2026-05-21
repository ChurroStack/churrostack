using ChurrunKubernetes.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrunKubernetes.Commands.Template
{
    public class GetTemplate : IRequest<GetTemplate, ValueTask<TemplateSummary>>
    {
        public string Name { get; private set; }

        public GetTemplate(string name)
        {
            Name = name;
        }
    }
}
