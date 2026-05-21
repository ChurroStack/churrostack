using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationDeployments : IRequest<GetApplicationDeployments, ValueTask<QueryResult<ApplicationDeploymentItem>>>
    {
        public string Name { get; private set; }
        public QueryRequest Query { get; private set; }

        public GetApplicationDeployments(string name, QueryRequest query)
        {
            Name = name;
            Query = query;
        }
    }
}
