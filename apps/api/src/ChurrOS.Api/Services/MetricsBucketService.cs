using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Services
{
    /// <inheritdoc />
    public class MetricsBucketService : IMetricsBucketService
    {
        private readonly ChurrosDbContext _context;
        private readonly ILogger<MetricsBucketService> _logger;

        public MetricsBucketService(ChurrosDbContext context, ILogger<MetricsBucketService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MetricValuesItem> BuildBucketedSeriesAsync(
            string metricName,
            IDictionary<string, string> responseLabels,
            List<MetricSeriesInfo> metrics,
            DateTimeOffset? requestFrom,
            DateTimeOffset? requestTo,
            string? tz,
            CancellationToken cancellationToken)
        {
            // Callers are expected to handle their own no-series early-return; we only get here
            // with a non-empty list. Defensive guard kept so a future caller can't accidentally
            // explode on an empty input.
            if (metrics.Count == 0)
            {
                return new MetricValuesItem(metricName, responseLabels, []);
            }

            // Set the time range window (UTC defaults so behavior is independent of the server's TZ).
            var from = requestFrom ?? DateTime.UtcNow.Date;
            var to = requestTo ?? DateTime.UtcNow.Date.AddDays(1);

            if (from >= to)
            {
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");
            }

            var metricsIds = metrics.Select(o => o.MetricId).ToList();

            // Get all metric values (for all matching series) for the given time range.
            // Include a 5-minute lookback so the counter Rate() has a predecessor sample for the first in-range bucket.
            var dateFrom = from.AddMinutes(-5);
            var metricValues = await _context.Set<MetricValue>()
                .Where(o => metricsIds.Contains(o.MetricId) && dateFrom <= o.Timestamp && o.Timestamp <= to)
                .Select(o => new MetricValueEntry(o.MetricId, o.Timestamp, o.Value))
                .ToListAsync(cancellationToken);

            // All series share the same metric type (Counter/Gauge/...); take it from the first one.
            var metricType = metrics[0].Type;
            if (metricType == MetricType.Histogram || metricType == MetricType.Summary)
            {
                throw new NotImplementedException("Histogram and Summary metric types are not supported yet.");
            }

            if (metricValues.Count == 0)
            {
                _logger.LogDebug("MetricsBucketService no values: metric={MetricName}", metricName);
                return new MetricValuesItem(metricName, responseLabels, []);
            }

            // For counter metrics, we need to calculate the rate of change.
            if (metricType == MetricType.Counter)
            {
                metricValues = metricValues.Rate();
            }

            // Resolve the caller's timezone so daily/hourly buckets align with their local clock.
            // Falls back to UTC for missing/unknown ids (e.g. typo, sandboxed browser).
            var timeZone = TimeZoneInfo.Utc;
            if (!string.IsNullOrEmpty(tz))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(tz);
                }
                catch (TimeZoneNotFoundException)
                {
                    _logger.LogWarning("MetricsBucketService unknown tz='{Tz}', falling back to UTC (metric={MetricName})", tz, metricName);
                }
                catch (InvalidTimeZoneException)
                {
                    _logger.LogWarning("MetricsBucketService invalid tz='{Tz}', falling back to UTC (metric={MetricName})", tz, metricName);
                }
            }

            // Pick bucket size based on the window length (3-tier ladder).
            var diff = to - from;
            TimeSpan bucketSize;
            if (diff.TotalDays > 1)
                bucketSize = TimeSpan.FromDays(1);
            else if (diff.TotalHours > 1)
                bucketSize = TimeSpan.FromHours(1);
            else
                bucketSize = TimeSpan.FromMinutes(1);

            var finalMetrics = metricValues.AdjustOverTime(metricType, from, to, bucketSize, timeZone);

            return new MetricValuesItem(metricName, responseLabels, finalMetrics.ToArray());
        }
    }
}
