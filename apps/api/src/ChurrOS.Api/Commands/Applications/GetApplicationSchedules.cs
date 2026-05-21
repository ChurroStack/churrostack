using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationSchedules : IRequest<GetApplicationSchedules, ValueTask<QueryResult<ApplicationScheduleItem>>>
    {
        public string Name { get; private set; }

        public GetApplicationSchedules(string name)
        {
            Name = name;
        }
    }
}
