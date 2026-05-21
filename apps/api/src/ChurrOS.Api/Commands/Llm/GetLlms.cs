using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlms : IRequest<GetLlms, ValueTask<QueryResult<LlmSummary>>>
    {
        public class LlmsQueryRequest : QueryRequest
        {
            public LlmsQueryRequest() : base() { }

            public LlmsQueryRequest(int? page = DefaultPage, int? pageSize = DefaultPageSize, string? search = null) : base(page, pageSize, search)
            {
            }
        }

        public LlmsQueryRequest Query { get; private set; }

        public GetLlms(LlmsQueryRequest query)
        {
            Query = query;
        }
    }
}
