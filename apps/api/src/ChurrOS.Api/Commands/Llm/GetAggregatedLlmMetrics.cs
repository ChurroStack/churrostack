using ChurrOS.Api.Models.Dtos.Metrics;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    /// <summary>
    /// Cross-LLM aggregated metric query. Same shape as <see cref="GetLlmMetrics"/> minus the
    /// per-LLM scoping; the handler resolves the set of LLMs the current identity can read and
    /// sums metric values across them.
    /// </summary>
    public class GetAggregatedLlmMetrics : IRequest<GetAggregatedLlmMetrics, ValueTask<MetricValuesItem>>
    {
        public string MetricName { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }
        public string? Tz { get; private set; }
        public string? IdentityName { get; private set; }
        public string? UserId { get; private set; }
        public string? Model { get; private set; }

        public GetAggregatedLlmMetrics(string metricName, DateTimeOffset? from, DateTimeOffset? to, string? tz = null, string? identityName = null, string? userId = null, string? model = null)
        {
            MetricName = metricName;
            From = from;
            To = to;
            Tz = tz;
            IdentityName = identityName;
            UserId = userId;
            Model = model;
        }
    }
}
