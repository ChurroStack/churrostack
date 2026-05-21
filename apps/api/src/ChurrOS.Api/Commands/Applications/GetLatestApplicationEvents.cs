using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetLatestApplicationEvents : IRequest<GetLatestApplicationEvents, ValueTask<QueryResult<ApplicationEventItem>>>
    {
        public string Name { get; private set; }
        public int? Take { get; set; }
        public string? Search { get; set; }

        public GetLatestApplicationEvents(string name, int? take = null, string? search = null)
        {
            Name = name;
            Search = search;
        }
    }
}
