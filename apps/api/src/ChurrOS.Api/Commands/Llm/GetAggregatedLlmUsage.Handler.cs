using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetAggregatedLlmUsageHandler : IRequestHandler<GetAggregatedLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        private static readonly HashSet<string> AllowedGroupBys = new(StringComparer.Ordinal)
        {
            "identity_name", "destination_model", "x_user_id"
        };

        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ILogger<GetAggregatedLlmUsageHandler> _logger;

        public GetAggregatedLlmUsageHandler(IMediator mediator, ChurrosDbContext context, ILogger<GetAggregatedLlmUsageHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async ValueTask<QueryResult<LlmUsageItem>> Handle(GetAggregatedLlmUsage request, CancellationToken cancellationToken)
        {
            var groupBy = string.IsNullOrWhiteSpace(request.GroupBy) ? "identity_name" : request.GroupBy;
            if (!AllowedGroupBys.Contains(groupBy))
            {
                throw new ArgumentException($"Invalid groupBy '{groupBy}'. Allowed: identity_name, destination_model, x_user_id.");
            }

            // Resolve accessible LLMs (with Destinations for pricing). IDs stay as strings end-to-end
            // because IdGen snowflakes exceed JS's 2^53 Number range; no contract surface for this
            // data may serialize them as numbers.
            var accessibleLlms = await GetAccessibleLlmsWithDestinationsAsync(cancellationToken);
            var accessibleIds = accessibleLlms.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);

            _logger.LogInformation(
                "llm.aggregated_usage groupBy={GroupBy} llmCount={LlmCount} hasFromFilter={HasFrom} hasToFilter={HasTo} hasIdentityFilter={HasIdentity} hasUserIdFilter={HasUserId} hasModelFilter={HasModel}",
                groupBy,
                accessibleIds.Count,
                request.From.HasValue,
                request.To.HasValue,
                !string.IsNullOrWhiteSpace(request.IdentityName),
                !string.IsNullOrWhiteSpace(request.UserId),
                !string.IsNullOrWhiteSpace(request.Model));

            if (accessibleIds.Count == 0)
            {
                return new QueryResult<LlmUsageItem>(new List<LlmUsageItem>());
            }

            // Merge per-destination pricing across every accessible LLM (first-wins on collisions).
            var (priceByHostModel, priceByModel) = LlmUsageAggregator.BuildPriceMaps(
                accessibleLlms.SelectMany(l => l.Destination ?? Array.Empty<LLmDestinationItem>()));

            // Base labels for the series fetch: no llm_id (we span all accessible LLMs).
            var baseLabels = new Dictionary<string, string>();
            AddIfPresent(baseLabels, "identity_name", request.IdentityName);
            AddIfPresent(baseLabels, "x_user_id", request.UserId);
            AddIfPresent(baseLabels, "destination_model", request.Model);

            var promptSeries = await FetchAccessibleSeriesAsync("prompt_tokens", baseLabels, accessibleIds, request, cancellationToken);
            var completionSeries = await FetchAccessibleSeriesAsync("completion_tokens", baseLabels, accessibleIds, request, cancellationToken);
            var countSeries = await FetchAccessibleSeriesAsync("completion_count", baseLabels, accessibleIds, request, cancellationToken);

            var rows = LlmUsageAggregator.BuildRows(
                groupBy,
                promptSeries,
                completionSeries,
                countSeries,
                priceByHostModel,
                priceByModel);
            rows = LlmUsageAggregator.Sort(rows, request.OrderBy, request.OrderDirection);

            _logger.LogInformation("llm.aggregated_usage groupBy={GroupBy} rows={RowCount}", groupBy, rows.Count);

            return new QueryResult<LlmUsageItem>(rows);
        }

        private async Task<List<MetricSeriesTotal>> FetchAccessibleSeriesAsync(
            string metricName,
            IDictionary<string, string> baseLabels,
            HashSet<string> accessibleIds,
            GetAggregatedLlmUsage request,
            CancellationToken cancellationToken)
        {
            var labels = new Dictionary<string, string>(baseLabels) { { "metric", metricName } };
            try
            {
                var series = await _mediator.Send(new GetMetricSeriesTotals(labels, request.From, request.To), cancellationToken);
                // Post-filter to series whose llm_id label is in the accessible set. Missing/empty
                // labels simply don't match — no parsing, no throwing.
                return series
                    .Where(s => s.Labels.TryGetValue("llm_id", out var id) && !string.IsNullOrEmpty(id) && accessibleIds.Contains(id))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "llm.aggregated_usage no series for metric={Metric}", metricName);
                return new List<MetricSeriesTotal>();
            }
        }

        private async Task<List<AccessibleLlm>> GetAccessibleLlmsWithDestinationsAsync(CancellationToken cancellationToken)
        {
            // Project Id to string at the DB boundary so we never widen IdGen snowflakes to long
            // anywhere downstream (JS Number precision loss above 2^53).
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (isAdmin)
            {
                var rows = await _context.Set<Domain.Llm>()
                    .AsNoTracking()
                    .Select(o => new { o.Id, o.Destination })
                    .ToListAsync(cancellationToken);
                return rows.Select(r => new AccessibleLlm(r.Id.ToString(), r.Destination)).ToList();
            }

            var acls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
            if (acls.Count == 0) return new List<AccessibleLlm>();

            var aclIds = acls.Keys.ToArray();
            var aclRows = await _context.Set<Domain.Llm>()
                .AsNoTracking()
                .Where(o => aclIds.Contains(o.AclId))
                .Select(o => new { o.Id, o.Destination })
                .ToListAsync(cancellationToken);
            return aclRows.Select(r => new AccessibleLlm(r.Id.ToString(), r.Destination)).ToList();
        }

        private static void AddIfPresent(IDictionary<string, string> labels, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                labels[key] = value;
            }
        }

        private sealed record AccessibleLlm(string Id, LLmDestinationItem[]? Destination);
    }
}
