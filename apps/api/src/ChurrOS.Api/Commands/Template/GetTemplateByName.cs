using ChurrOS.Api.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class GetTemplateByName : IRequest<GetTemplateByName, ValueTask<TemplateItem>>
    {
        public string Name { get; private set; }
        public string Target { get; private set; }

        public GetTemplateByName(string name, string target)
        {
            Name = name;
            Target = target;
        }
    }
}
