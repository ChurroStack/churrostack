using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class UpdateEnvironmentJobs : IRequest<UpdateEnvironmentJobs, Task>
    {
        public string Name { get; private set; }

        public UpdateEnvironmentJobs(string name)
        {
            Name = name;
        }
    }
}
