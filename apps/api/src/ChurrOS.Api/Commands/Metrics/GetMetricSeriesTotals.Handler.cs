using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricSeriesTotalsHandler : IRequestHandler<GetMetricSeriesTotals, ValueTask<List<MetricSeriesTotal>>>
    {
        private readonly ChurrosDbContext _context;

        public GetMetricSeriesTotalsHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<List<MetricSeriesTotal>> Handle(GetMetricSeriesTotals request, CancellationToken cancellationToken)
        {
            var metrics = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, request.Labels))
                .Select(o => new { o.MetricId, o.Type, o.Labels })
                .ToListAsync(cancellationToken);

            if (metrics.Count == 0)
                return new List<MetricSeriesTotal>();

            var metricsLabels = metrics.ToDictionary(o => o.MetricId, o => o.Labels);
            var metricType = metrics[0].Type;
            if (metricType == MetricType.Histogram || metricType == MetricType.Summary)
            {
                throw new NotImplementedException("Histogram and Summary metric types are not supported yet.");
            }

            var from = request.From ?? DateTime.UtcNow.Date;
            var to = request.To ?? DateTime.UtcNow.Date.AddDays(1);

            if (from >= to)
            {
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");
            }

            var metricsIds = metrics.Select(o => o.MetricId).ToList();
            var dateFrom = from.AddMinutes(-5);
            var metricValues = await _context.Set<MetricValue>()
                .Where(o => metricsIds.Contains(o.MetricId) && dateFrom <= o.Timestamp && o.Timestamp <= to)
                .Select(o => new MetricValueEntry(o.MetricId, o.Timestamp, o.Value))
                .ToListAsync(cancellationToken);

            if (metricValues.Count == 0)
                return new List<MetricSeriesTotal>();

            if (metricType == MetricType.Counter)
            {
                metricValues = metricValues.Rate();
            }

            // Sum per series; Rate() already buckets per-MetricId so a sum here yields the total counter
            // increase across the window. For Gauges, sum without rate matches the existing behaviour.
            return metricValues
                .GroupBy(m => m.MetricId)
                .Select(g => new MetricSeriesTotal(metricsLabels[g.Key], g.Sum(x => x.Value)))
                .ToList();
        }
    }
}
