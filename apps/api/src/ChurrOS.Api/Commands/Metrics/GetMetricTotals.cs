using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricTotals : IRequest<GetMetricTotals, ValueTask<IDictionary<string, double>>>
    {
        public IDictionary<string, string> Labels { get; private set; }
        public string GroupBy { get; set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetMetricTotals(IDictionary<string, string> labels, string groupBy, DateTimeOffset? from, DateTimeOffset? to)
        {
            Labels = labels;
            GroupBy = groupBy;
            From = from;
            To = to;
        }
    }
}
