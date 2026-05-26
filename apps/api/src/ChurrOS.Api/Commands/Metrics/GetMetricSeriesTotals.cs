using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Metrics
{
    public record MetricSeriesTotal(IDictionary<string, string> Labels, double Total);

    public class GetMetricSeriesTotals : IRequest<GetMetricSeriesTotals, ValueTask<List<MetricSeriesTotal>>>
    {
        public IDictionary<string, string> Labels { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetMetricSeriesTotals(IDictionary<string, string> labels, DateTimeOffset? from, DateTimeOffset? to)
        {
            Labels = labels;
            From = from;
            To = to;
        }
    }
}
