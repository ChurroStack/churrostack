using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Models.Dtos.Metrics;

namespace ChurrOS.Api.Services
{
    /// <summary>
    /// Shared metric bucketing pipeline. Both <c>GetMetricsHandler</c> (per-LLM /
    /// label-scoped query) and <c>GetAggregatedLlmMetricsHandler</c> (cross-LLM with
    /// access-filtered series) depend on this so the rate + AdjustOverTime logic lives
    /// in exactly one place.
    /// </summary>
    public interface IMetricsBucketService
    {
        Task<MetricValuesItem> BuildBucketedSeriesAsync(
            string metricName,
            IDictionary<string, string> responseLabels,
            List<MetricSeriesInfo> metrics,
            DateTimeOffset? requestFrom,
            DateTimeOffset? requestTo,
            string? tz,
            CancellationToken cancellationToken);

        Task<MetricValuesItem> BuildPeakPerMinuteSeriesAsync(
            string metricName,
            IDictionary<string, string> responseLabels,
            List<MetricSeriesInfo> metrics,
            DateTimeOffset? requestFrom,
            DateTimeOffset? requestTo,
            string? tz,
            CancellationToken cancellationToken);
    }
}
