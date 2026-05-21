using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class DeleteEnvironmentJobs : IRequest<DeleteEnvironmentJobs, Task>
    {
        public string Name { get; private set; }

        public DeleteEnvironmentJobs(string name)
        {
            Name = name;
        }
    }
}
