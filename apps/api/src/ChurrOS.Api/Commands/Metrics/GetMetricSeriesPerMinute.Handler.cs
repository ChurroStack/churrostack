using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricSeriesPerMinuteHandler : IRequestHandler<GetMetricSeriesPerMinute, ValueTask<List<MetricSeriesMinute>>>
    {
        private readonly ChurrosDbContext _context;

        public GetMetricSeriesPerMinuteHandler(ChurrosDbContext context)
        {
            _context = context;
        }

        public async ValueTask<List<MetricSeriesMinute>> Handle(GetMetricSeriesPerMinute request, CancellationToken cancellationToken)
        {
            var metrics = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, request.Labels))
                .Select(o => new { o.MetricId, o.Type, o.Labels })
                .ToListAsync(cancellationToken);

            if (metrics.Count == 0)
                return new List<MetricSeriesMinute>();

            var metricsLabels = metrics.ToDictionary(o => o.MetricId, o => o.Labels);
            var metricType = metrics[0].Type;
            if (metricType == MetricType.Histogram || metricType == MetricType.Summary)
                throw new NotImplementedException("Histogram and Summary metric types are not supported yet.");

            var from = request.From ?? DateTime.UtcNow.Date;
            var to = request.To ?? DateTime.UtcNow.Date.AddDays(1);

            if (from >= to)
                throw new ArgumentException("The 'From' date must be earlier than the 'To' date.");

            var metricsIds = metrics.Select(o => o.MetricId).ToList();
            var dateFrom = from.AddMinutes(-5);
            var metricValues = await _context.Set<MetricValue>()
                .Where(o => metricsIds.Contains(o.MetricId) && dateFrom <= o.Timestamp && o.Timestamp <= to)
                .Select(o => new MetricValueEntry(o.MetricId, o.Timestamp, o.Value))
                .ToListAsync(cancellationToken);

            if (metricValues.Count == 0)
                return new List<MetricSeriesMinute>();

            if (metricType == MetricType.Counter)
                metricValues = metricValues.Rate();

            // Return per-series per-minute rows. Exclude the -5 min lookback buckets so they
            // cannot be a spurious peak candidate (Rate() needs them but they predate the window).
            return metricValues
                .Where(m => m.Timestamp >= from)
                .Select(m => new MetricSeriesMinute(metricsLabels[m.MetricId], m.Timestamp, m.Value))
                .ToList();
        }
    }
}
