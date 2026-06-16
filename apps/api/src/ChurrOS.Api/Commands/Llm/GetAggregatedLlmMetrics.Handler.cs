using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetAggregatedLlmMetricsHandler : IRequestHandler<GetAggregatedLlmMetrics, ValueTask<MetricValuesItem>>
    {
        private static readonly Dictionary<string, string[]> RateMetricMap = new(StringComparer.Ordinal)
        {
            { "requests_per_minute", new[] { "completion_count" } },
            { "tokens_per_minute", new[] { "prompt_tokens", "completion_tokens" } },
        };

        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly IMetricsBucketService _bucketService;
        private readonly ILogger<GetAggregatedLlmMetricsHandler> _logger;

        public GetAggregatedLlmMetricsHandler(IMediator mediator, ChurrosDbContext context, IMetricsBucketService bucketService, ILogger<GetAggregatedLlmMetricsHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _bucketService = bucketService;
            _logger = logger;
        }

        public async ValueTask<MetricValuesItem> Handle(GetAggregatedLlmMetrics request, CancellationToken cancellationToken)
        {
            // Build the response labels once and reuse them for both the empty-access and the
            // success branches so the response shape is consistent.
            var labels = new Dictionary<string, string>
            {
                { "metric", request.MetricName }
            };
            if (!string.IsNullOrWhiteSpace(request.IdentityName)) labels["identity_name"] = request.IdentityName;
            if (!string.IsNullOrWhiteSpace(request.UserId)) labels["x_user_id"] = request.UserId;
            if (!string.IsNullOrWhiteSpace(request.Model)) labels["destination_model"] = request.Model;

            // Resolve accessible LLM IDs as strings. IDs are IdGen snowflakes (63-bit longs); we
            // never widen them to long because Metric.Labels["llm_id"] is a string, and downstream
            // JSON contracts that might surface this set must stay JS-precision-safe.
            var accessible = await GetAccessibleLlmIdsAsync(cancellationToken);
            if (accessible.Count == 0)
            {
                _logger.LogInformation("llm.aggregated_metrics metric={MetricName} llmCount=0 reason=no_access", request.MetricName);
                return new MetricValuesItem(request.MetricName, labels, []);
            }

            if (RateMetricMap.TryGetValue(request.MetricName, out var underlyingMetrics))
            {
                // Synthetic rate metric: fetch underlying stored metrics, filter by accessible llm_ids,
                // combine, and build a peak-per-minute series. No stored metric named after this key exists.
                var combinedSeries = new List<MetricSeriesInfo>();
                foreach (var underlying in underlyingMetrics)
                {
                    var underlyingLabels = new Dictionary<string, string>(labels) { ["metric"] = underlying };
                    var series = await _context.Set<Metric>()
                        .Where(o => EF.Functions.JsonContains(o.Labels, underlyingLabels))
                        .Select(o => new MetricSeriesInfo(o.MetricId, o.Type, o.Labels))
                        .ToListAsync(cancellationToken);
                    combinedSeries.AddRange(
                        series.Where(o => o.Labels.TryGetValue("llm_id", out var s) && !string.IsNullOrEmpty(s) && accessible.Contains(s)));
                }

                _logger.LogInformation(
                    "llm.aggregated_metrics metric={MetricName} llmCount={LlmCount} combinedSeries={SeriesCount}",
                    request.MetricName, accessible.Count, combinedSeries.Count);

                return await _bucketService.BuildPeakPerMinuteSeriesAsync(
                    request.MetricName,
                    labels,
                    combinedSeries,
                    request.From,
                    request.To,
                    request.Tz,
                    cancellationToken);
            }

            // Fetch all metric series matching the labels (no llm_id filter), then drop any series
            // whose llm_id label is not in the accessible set. Missing/empty llm_id labels simply
            // don't match — no parsing layer that could throw.
            var allSeries = await _context.Set<Metric>()
                .Where(o => EF.Functions.JsonContains(o.Labels, labels))
                .Select(o => new MetricSeriesInfo(o.MetricId, o.Type, o.Labels))
                .ToListAsync(cancellationToken);

            var filteredSeries = allSeries
                .Where(o => o.Labels.TryGetValue("llm_id", out var s) && !string.IsNullOrEmpty(s) && accessible.Contains(s))
                .ToList();

            _logger.LogInformation(
                "llm.aggregated_metrics metric={MetricName} llmCount={LlmCount} series={SeriesCount} filtered={FilteredCount}",
                request.MetricName, accessible.Count, allSeries.Count, filteredSeries.Count);

            return await _bucketService.BuildBucketedSeriesAsync(
                request.MetricName,
                labels,
                filteredSeries,
                request.From,
                request.To,
                request.Tz,
                cancellationToken);
        }

        private async Task<HashSet<string>> GetAccessibleLlmIdsAsync(CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (isAdmin)
            {
                var allIds = await _context.Set<Domain.Llm>()
                    .AsNoTracking()
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);
                return allIds.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
            }

            var acls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
            if (acls.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

            var aclIds = acls.Keys.ToArray();
            var ids = await _context.Set<Domain.Llm>()
                .AsNoTracking()
                .Where(o => aclIds.Contains(o.AclId))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            return ids.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        }
    }
}
