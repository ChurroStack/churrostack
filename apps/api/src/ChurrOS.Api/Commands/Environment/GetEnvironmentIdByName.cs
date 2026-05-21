using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentIdByName : IRequest<GetEnvironmentIdByName, ValueTask<EnvironmentIdentifier>>
    {
        public string Name { get; private set; }

        public GetEnvironmentIdByName(string name)
        {
            Name = name;
        }
    }
}
