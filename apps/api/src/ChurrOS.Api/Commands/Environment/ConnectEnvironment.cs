using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class ConnectEnvironment : IRequest<ConnectEnvironment, Task>
    {
        public string Name { get; private set; }

        public ConnectEnvironment(string name)
        {
            Name = name;
        }
    }
}
