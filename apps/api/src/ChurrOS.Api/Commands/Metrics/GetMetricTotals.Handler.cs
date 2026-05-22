using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricTotalsHandler : IRequestHandler<GetMetricTotals, ValueTask<IDictionary<string, double>>>
    {
        private readonly ChurrosDbContext _context;

        public GetMetricTotalsHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<IDictionary<string, double>> Handle(GetMetricTotals request, CancellationToken cancellationToken)
        {
            // Get all series for the given metric for the application
            var metrics = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, request.Labels))
                .Select(o => new { o.MetricId, o.Type, o.Labels })
                .ToListAsync();

            var metricsLabels = metrics.ToDictionary(o => o.MetricId, o => o.Labels);

            var metricName = request.Labels["metric"];

            if (metrics == null || metrics.Count == 0)
            {
                throw new NotFoundException($"Metric with name '{metricName}' was not found.");
            }

            // Set the time range window
            var from = request.From ?? DateTime.Today;
            var to = request.To ?? DateTime.Today.AddDays(1);

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
                .ToListAsync();

            // Get metric type (counter, gauge, histogram, summary)
            var metric = metrics.First();
            var metricType = metric.Type;
            if (metricType == Models.Dtos.MetricType.Histogram || metricType == Models.Dtos.MetricType.Summary)
            {
                throw new NotImplementedException("Histogram and Summary metric types are not supported yet.");
            }

            // If there are no values, return empty result
            if (metricValues.Count == 0)
                return new Dictionary<string, double>();

            // For counter metrics, we need to calculate the rate of change
            if (metricType == Models.Dtos.MetricType.Counter)
            {
                metricValues = metricValues.Rate();
            }

            // Calculate the granularity based on the time range
            var diff = to - from;
            var finalMetrics = new List<MetricValueItem>();
            if (diff.TotalDays > 1)
            {
                return GroupedAvgOverTime(metricValues, metricType, request.GroupBy, metricsLabels, from, to, "yyyyMMdd");
            }
            else if (diff.TotalHours > 1)
            {
                return GroupedAvgOverTime(metricValues, metricType, request.GroupBy, metricsLabels, from, to, "yyyyMMddHH");
            }
            else
            {
                return GroupedAvgOverTime(metricValues, metricType, request.GroupBy, metricsLabels, from, to);
            }
        }

        private static IDictionary<string, double> GroupedAvgOverTime(List<MetricValueEntry> metricValues, MetricType metricType, string groupBy, IDictionary<long, IDictionary<string, string>> metricsLabels, DateTimeOffset start, DateTimeOffset end, string aggregateBy = "yyyyMMddHHmm")
        {
            // Get time range in local timezone
            var from = start.LocalDateTime;
            var to = end.LocalDateTime;
            var now = DateTime.Now;

            // Calculate the sum(average(values by serie) by the time bucket)
            return metricValues
                .GroupBy(m => m.Timestamp.LocalDateTime.ToString(aggregateBy))
                .SelectMany(dateGroup => dateGroup
                    .GroupBy(x => x.MetricId)
                    .Select(metricGroup => new
                    {
                        MetricId = metricGroup.Key,
                        Date = dateGroup.Key,
                        Value = metricType == MetricType.Counter ? metricGroup.Sum(x => x.Value) : metricGroup.Average(x => x.Value)
                    })
                )
                .GroupBy(x => metricsLabels[x.MetricId][groupBy])
                .Select(metricGroup => new
                {
                    GroupBy = metricGroup.Key,
                    Value = metricGroup.Sum(x => x.Value)
                })
                .ToDictionary(g => g.GroupBy, g => g.Value);
        }
    }
}
