using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Template
{
    public class DeleteTemplate : IRequest<DeleteTemplate, Task>
    {
        public string Name { get; private set; }
        public string Target { get; private set; }

        public DeleteTemplate(string name, string target)
        {
            Name = name;
            Target = target;
        }
    }
}
