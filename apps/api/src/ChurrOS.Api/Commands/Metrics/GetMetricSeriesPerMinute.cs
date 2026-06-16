using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Metrics
{
    public record MetricSeriesMinute(IDictionary<string, string> Labels, DateTimeOffset Minute, double Value);

    public class GetMetricSeriesPerMinute : IRequest<GetMetricSeriesPerMinute, ValueTask<List<MetricSeriesMinute>>>
    {
        public IDictionary<string, string> Labels { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetMetricSeriesPerMinute(IDictionary<string, string> labels, DateTimeOffset? from, DateTimeOffset? to)
        {
            Labels = labels;
            From = from;
            To = to;
        }
    }
}
