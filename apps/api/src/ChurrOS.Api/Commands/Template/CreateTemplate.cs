using ChurrOS.Api.Models.Dtos.Template;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class CreateTemplate : IRequest<CreateTemplate, ValueTask<TemplateItem>>
    {
        public string Content { get; private set; }

        public CreateTemplate(string content)
        {
            Content = content;
        }
    }
}
