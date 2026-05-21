using ChurrOS.Api.Models.Dtos.Metrics;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetrics : IRequest<GetMetrics, ValueTask<MetricValuesItem>>
    {
        public IDictionary<string, string> Labels { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetMetrics(IDictionary<string, string> labels, DateTimeOffset? from, DateTimeOffset? to)
        {
            Labels = labels;
            From = from;
            To = to;
        }
    }
}
