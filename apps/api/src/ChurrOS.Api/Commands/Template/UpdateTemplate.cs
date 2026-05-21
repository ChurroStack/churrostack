using ChurrOS.Api.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class UpdateTemplate : IRequest<UpdateTemplate, ValueTask<TemplateItem>>
    {
        public string Name { get; private set; }
        public string Target { get; private set; }
        public string Content { get; private set; }

        public UpdateTemplate(string name, string target, string content)
        {
            Name = name;
            Target = target;
            Content = content;
        }
    }
}
