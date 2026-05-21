using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class DeleteEnvironment : IRequest<DeleteEnvironment, Task>
    {
        public string Name { get; private set; }

        public DeleteEnvironment(string name)
        {
            Name = name;
        }
    }
}
