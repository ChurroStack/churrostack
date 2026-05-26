using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Metrics
{
    public class GetMetricsHandler : IRequestHandler<GetMetrics, ValueTask<MetricValuesItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMetricsBucketService _bucketService;
        private readonly ILogger<GetMetricsHandler> _logger;

        public GetMetricsHandler(ChurrosDbContext context, IMetricsBucketService bucketService, ILogger<GetMetricsHandler> logger)
        {
            _context = context;
            _bucketService = bucketService;
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
                .Select(o => new MetricSeriesInfo(o.MetricId, o.Type, o.Labels))
                .ToListAsync(cancellationToken);

            // No series recorded for this metric yet — return an empty result so the UI can render a
            // neutral "no data yet" placeholder rather than treating it as an error.
            if (metrics == null || metrics.Count == 0)
            {
                _logger.LogDebug("GetMetrics empty: metric={MetricName} labels={Labels} reason=no_series", metricName, labelsLog);
                return new MetricValuesItem(metricName, request.Labels, []);
            }

            return await _bucketService.BuildBucketedSeriesAsync(
                metricName,
                request.Labels,
                metrics,
                request.From,
                request.To,
                request.Tz,
                cancellationToken);
        }
    }
}
