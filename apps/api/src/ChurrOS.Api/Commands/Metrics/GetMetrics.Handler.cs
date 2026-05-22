using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricsHandler : IRequestHandler<GetMetrics, ValueTask<MetricValuesItem>>
    {
        private readonly ChurrosDbContext _context;

        public GetMetricsHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<MetricValuesItem> Handle(GetMetrics request, CancellationToken cancellationToken)
        {
            // Get all series for the given metric for the application
            var metrics = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, request.Labels))
                .Select(o => new { o.MetricId, o.Type, o.Labels })
                .ToListAsync();

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

            // Get all metric values (for all previous series) for the given time range
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
                return new MetricValuesItem(metricName, request.Labels, []);

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
                finalMetrics = metricValues.AdjustOverTime(metricType, from, to, "yyyyMMdd");
            }
            else if (diff.TotalHours > 1)
            {
                finalMetrics = metricValues.AdjustOverTime(metricType, from, to, "yyyyMMddHH");
            }
            else
            {
                finalMetrics = metricValues.AdjustOverTime(metricType, from, to);
            }

            return new MetricValuesItem(metricName, request.Labels, finalMetrics.ToArray());
        }
    }
}
