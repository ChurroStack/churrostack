using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class RotateEnvironmentKeys : IRequest<RotateEnvironmentKeys, ValueTask<EnvironmentKeysItem>>
    {
        public string Name { get; private set; }

        public RotateEnvironmentKeys(string name)
        {
            Name = name;
        }
    }
}
