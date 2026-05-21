using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationIdByName : IRequest<GetApplicationIdByName, ValueTask<ApplicationIdentifier>>
    {
        public string Name { get; private set; }

        public GetApplicationIdByName(string name)
        {
            Name = name;
        }
    }
}
