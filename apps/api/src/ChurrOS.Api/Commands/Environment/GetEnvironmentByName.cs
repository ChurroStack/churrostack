using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentByName : IRequest<GetEnvironmentByName, ValueTask<EnvironmentItem>>
    {
        public string Name { get; private set; }

        public GetEnvironmentByName(string name)
        {
            Name = name;
        }
    }
}
