using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironments : IRequest<GetEnvironments, ValueTask<QueryResult<EnvironmentSummary>>>
    {
        public QueryRequest Query { get; private set; }

        public GetEnvironments(QueryRequest query)
        {
            Query = query;
        }
    }
}
