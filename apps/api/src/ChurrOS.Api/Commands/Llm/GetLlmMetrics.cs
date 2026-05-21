using ChurrOS.Api.Models.Dtos.Metrics;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmMetrics : IRequest<GetLlmMetrics, ValueTask<MetricValuesItem>>
    {
        public long LlmId { get; private set; }
        public string MetricName { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetLlmMetrics(long llmId, string metricName, DateTimeOffset? from, DateTimeOffset? to)
        {
            LlmId = llmId;
            MetricName = metricName;
            From = from;
            To = to;
        }
    }
}
