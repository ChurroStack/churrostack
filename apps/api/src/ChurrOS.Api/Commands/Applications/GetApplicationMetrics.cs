using ChurrOS.Api.Models.Dtos.Metrics;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationMetrics : IRequest<GetApplicationMetrics, ValueTask<MetricValuesItem>>
    {
        public string AppName { get; private set; }
        public string MetricName { get; private set; }
        public DateTimeOffset? From { get; private set; }
        public DateTimeOffset? To { get; private set; }

        public GetApplicationMetrics(string appName, string metricName, DateTimeOffset? from, DateTimeOffset? to)
        {
            AppName = appName;
            MetricName = metricName;
            From = from;
            To = to;
        }
    }
}
