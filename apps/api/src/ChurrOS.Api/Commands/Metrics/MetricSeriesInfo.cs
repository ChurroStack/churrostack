using ChurrOS.Api.Models.Dtos;

namespace ChurrOS.Api.Commands.Metrics
{
    /// <summary>
    /// Light projection of a metric series. Used by callers that fetch series themselves
    /// (with their own JsonContains query + any post-filtering) and pass the result into
    /// <c>IMetricsBucketService.BuildBucketedSeriesAsync</c>.
    /// </summary>
    public record MetricSeriesInfo(long MetricId, MetricType Type, IDictionary<string, string> Labels);
}
