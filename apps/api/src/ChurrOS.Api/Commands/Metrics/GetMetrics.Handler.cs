using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricsHandler : IRequestHandler<GetMetrics, ValueTask<MetricValuesItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly ILogger<GetMetricsHandler> _logger;

        public GetMetricsHandler(ChurrosDbContext context, ILogger<GetMetricsHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async ValueTask<MetricValuesItem> Handle(GetMetrics request, CancellationToken cancellationToken)
        {
            var metricName = request.Labels["metric"];
            // Pre-formatted for structured logs: the default MEL formatter renders IDictionary as its type name.
            var labelsLog = string.Join(",", request.Labels.Select(kv => $"{kv.Key}={kv.Value}"));

            // Get all series for the given metric for the application
            var metrics = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, request.Labels))
                .Select(o => new { o.MetricId, o.Type, o.Labels })
                .ToListAsync(cancellationToken);

            // No series recorded for this metric yet — return an empty result so the UI can render a
            // neutral "no data yet" placeholder rather than treating it as an error.
            if (metrics == null || metrics.Count == 0)
            {
                _logger.LogDebug("GetMetrics empty: metric={MetricName} labels={Labels} reason=no_series", metricName, labelsLog);
                return new MetricValuesItem(metricName, request.Labels, []);
            }

            // Set the time range window (UTC defaults so behavior is independent of the server's TZ).
            var from = request.From ?? DateTime.UtcNow.Date;
            var to = request.To ?? DateTime.UtcNow.Date.AddDays(1);

            if (from >= to)
            {
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");
            }

            var metricsIds = metrics.Select(o => o.MetricId).ToList();

            // Get all metric values (for all previous series) for the given time range.
            // Include a 5-minute lookback so the counter Rate() has a predecessor sample for the first in-range bucket.
            var dateFrom = from.AddMinutes(-5);
            var metricValues = await _context.Set<MetricValue>()
                .Where(o => metricsIds.Contains(o.MetricId) && dateFrom <= o.Timestamp && o.Timestamp <= to)
                .Select(o => new MetricValueEntry(o.MetricId, o.Timestamp, o.Value))
                .ToListAsync(cancellationToken);

            // Get metric type (counter, gauge, histogram, summary)
            var metric = metrics.First();
            var metricType = metric.Type;
            if (metricType == Models.Dtos.MetricType.Histogram || metricType == Models.Dtos.MetricType.Summary)
            {
                throw new NotImplementedException("Histogram and Summary metric types are not supported yet.");
            }

            // If there are no values, return empty result
            if (metricValues.Count == 0)
            {
                _logger.LogDebug("GetMetrics empty: metric={MetricName} labels={Labels} reason=no_values", metricName, labelsLog);
                return new MetricValuesItem(metricName, request.Labels, []);
            }

            // For counter metrics, we need to calculate the rate of change
            if (metricType == Models.Dtos.MetricType.Counter)
            {
                metricValues = metricValues.Rate();
            }

            // Resolve the caller's timezone so daily/hourly buckets align with their local clock.
            // Falls back to UTC for missing/unknown ids (e.g. typo, sandboxed browser).
            var timeZone = TimeZoneInfo.Utc;
            if (!string.IsNullOrEmpty(request.Tz))
            {
                try
                {
                    timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.Tz);
                }
                catch (TimeZoneNotFoundException)
                {
                    _logger.LogWarning("GetMetrics unknown tz='{Tz}', falling back to UTC (metric={MetricName})", request.Tz, metricName);
                }
                catch (InvalidTimeZoneException)
                {
                    _logger.LogWarning("GetMetrics invalid tz='{Tz}', falling back to UTC (metric={MetricName})", request.Tz, metricName);
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

            return new MetricValuesItem(metricName, request.Labels, finalMetrics.ToArray());
        }
    }
}
