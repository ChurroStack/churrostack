using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Llm;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    /// <summary>
    /// Cross-LLM aggregated usage query. Same shape as <see cref="GetLlmUsage"/> minus the per-LLM
    /// scoping; the handler resolves the set of LLMs the current identity can read and sums
    /// completions / tokens / spend across them.
    /// </summary>
    public class GetAggregatedLlmUsage : IRequest<GetAggregatedLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        public string GroupBy { get; private set; }
        public string OrderBy { get; private set; }
        public string OrderDirection { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public string? IdentityName { get; private set; }
        public string? UserId { get; private set; }
        public string? Model { get; private set; }

        public GetAggregatedLlmUsage(string groupBy, string orderBy, string orderDirection, DateTimeOffset? from, DateTimeOffset? to, string? identityName = null, string? userId = null, string? model = null)
        {
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
