using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.ApiKey;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.ApiKeys
{
    public class GetApiKeys : IRequest<GetApiKeys, ValueTask<QueryResult<ApiKeyItem>>>
    {
        public QueryRequest Query { get; private set; }

        public GetApiKeys(QueryRequest query)
        {
            Query = query;
        }
    }
}
