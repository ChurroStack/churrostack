using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Environment;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironments : IRequest<GetEnvironments, ValueTask<QueryResult<EnvironmentSummary>>>
    {
        public class EnvironmentQueryRequest : QueryRequest
        {
            public string[]? Tags { get; set; }

            public EnvironmentQueryRequest() : base() { }

            public EnvironmentQueryRequest(int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null, string[]? tags = null) : base(page, pageSize, search)
            {
                Tags = tags;
            }
        }

        public EnvironmentQueryRequest Query { get; private set; }

        public GetEnvironments(EnvironmentQueryRequest query)
        {
            Query = query;
        }
    }
}
