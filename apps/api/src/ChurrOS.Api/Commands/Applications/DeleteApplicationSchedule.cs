using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class DeleteApplicationSchedule : IRequest<DeleteApplicationSchedule, Task>
    {
        public string AppName { get; private set; }
        public string Name { get; private set; }

        public DeleteApplicationSchedule(string appName, string name)
        {
            AppName = appName;
            Name = name;
        }
    }
}
