using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationByName : IRequest<GetApplicationByName, ValueTask<ApplicationItem>>
    {
        public string Name { get; private set; }

        public GetApplicationByName(string name)
        {
            Name = name;
        }
    }
}
