using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmUsage : IRequest<GetLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        public long LlmId { get; private set; }
        public string GroupBy { get; private set; }
        public string OrderBy { get; private set; }
        public string OrderDirection { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public string? IdentityName { get; private set; }
        public string? UserId { get; private set; }
        public string? Model { get; private set; }

        public GetLlmUsage(long llmId, string groupBy, string orderBy, string orderDirection, DateTimeOffset? from, DateTimeOffset? to, string? identityName = null, string? userId = null, string? model = null)
        {
            LlmId = llmId;
            GroupBy = groupBy;
            OrderBy = orderBy;
            OrderDirection = orderDirection;
            From = from;
            To = to;
            IdentityName = identityName;
            UserId = userId;
            Model = model;
        }
    }
}
