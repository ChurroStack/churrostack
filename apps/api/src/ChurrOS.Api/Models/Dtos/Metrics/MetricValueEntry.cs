using System.Diagnostics;

namespace ChurrOS.Api.Models.Dtos.Metrics
{
    [DebuggerDisplay("{Value} - {Timestamp}")]
    internal record MetricValueEntry(long MetricId, DateTimeOffset Timestamp, double Value);
}
