using System.Diagnostics;

namespace ChurrOS.Api.Models.Dtos.Metrics
{
    [DebuggerDisplay("{Value} - {Timestamp}")]
    public class MetricValueItem
    {
        public DateTimeOffset Timestamp { get; set; }
        public double Value { get; set; }

        public MetricValueItem(DateTimeOffset timestamp, double value)
        {
            Timestamp = timestamp;
            Value = value;
        }
    }
}
