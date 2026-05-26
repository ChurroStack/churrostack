using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Llm
{
    public class GetLlmUsageHandler : IRequestHandler<GetLlmUsage, ValueTask<QueryResult<LlmUsageItem>>>
    {
        private static readonly HashSet<string> AllowedGroupBys = new(StringComparer.Ordinal)
        {
            "identity_name", "destination_model", "x_user_id"
        };

        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ILogger<GetLlmUsageHandler> _logger;

        public GetLlmUsageHandler(IMediator mediator, ChurrosDbContext context, ILogger<GetLlmUsageHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async ValueTask<QueryResult<LlmUsageItem>> Handle(GetLlmUsage request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Llm>();
            var item = await repo
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.LlmId, cancellationToken);

            if (item == null)
            {
                throw new NotFoundException($"LLm with id '{request.LlmId}' was not found.");
            }

            if (!await _mediator.Send(new IsAdminOrHasAcl(item.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException();

            var groupBy = string.IsNullOrWhiteSpace(request.GroupBy) ? "identity_name" : request.GroupBy;
            if (!AllowedGroupBys.Contains(groupBy))
            {
                throw new ArgumentException($"Invalid groupBy '{groupBy}'. Allowed: identity_name, destination_model, x_user_id.");
            }

            _logger.LogInformation(
                "llm.usage llmId={LlmId} groupBy={GroupBy} from={From} to={To} hasIdentityFilter={HasIdentity} hasUserIdFilter={HasUserId} hasModelFilter={HasModel}",
                request.LlmId,
                groupBy,
                request.From,
                request.To,
                !string.IsNullOrWhiteSpace(request.IdentityName),
                !string.IsNullOrWhiteSpace(request.UserId),
                !string.IsNullOrWhiteSpace(request.Model));

            // Build per-(host, model) pricing lookup from the LLM destinations.
            // Two destinations sharing the same host+model collapse: the first one wins.
            var priceByHostModel = new Dictionary<(string Host, string Model), (decimal InPer1M, decimal OutPer1M)>();
            foreach (var dest in item.Destination ?? Array.Empty<LLmDestinationItem>())
            {
                var host = TryGetHost(dest.Uri);
                var model = dest.Model ?? string.Empty;
                var key = (host, model);
                if (!priceByHostModel.ContainsKey(key))
                {
                    priceByHostModel[key] = (
                        dest.InputTokenPricePer1M ?? 0m,
                        dest.OutputTokenPricePer1M ?? 0m);
                }
            }

            var baseLabels = new Dictionary<string, string>
            {
                { "llm_id", item.Id.ToString() }
            };
            AddIfPresent(baseLabels, "identity_name", request.IdentityName);
            AddIfPresent(baseLabels, "x_user_id", request.UserId);
            AddIfPresent(baseLabels, "destination_model", request.Model);

            var promptSeries = await FetchSeriesSafeAsync("prompt_tokens", baseLabels, request, cancellationToken);
            var completionSeries = await FetchSeriesSafeAsync("completion_tokens", baseLabels, request, cancellationToken);
            var countSeries = await FetchSeriesSafeAsync("completion_count", baseLabels, request, cancellationToken);

            // Accumulator: (groupValue, host, model) -> tokens / completions.
            var buckets = new Dictionary<(string Group, string Host, string Model), Aggregate>();

            Accumulate(buckets, promptSeries, groupBy, (a, v) => a.PromptTokens += v);
            Accumulate(buckets, completionSeries, groupBy, (a, v) => a.CompletionTokens += v);
            Accumulate(buckets, countSeries, groupBy, (a, v) => a.Completions += v);

            // Collapse to one row per group value, applying per-(host,model) prices.
            var byGroup = new Dictionary<string, LlmUsageRow>();
            foreach (var ((group, host, model), agg) in buckets)
            {
                if (!byGroup.TryGetValue(group, out var row))
                {
                    row = new LlmUsageRow { Name = group };
                    byGroup[group] = row;
                }
                row.PromptTokens += (long)agg.PromptTokens;
                row.CompletionTokens += (long)agg.CompletionTokens;
                row.Completions += (long)agg.Completions;

                var price = priceByHostModel.TryGetValue((host, model), out var p) ? p : (InPer1M: 0m, OutPer1M: 0m);
                row.InputSpend += ((decimal)agg.PromptTokens) * price.InPer1M / 1_000_000m;
                row.OutputSpend += ((decimal)agg.CompletionTokens) * price.OutPer1M / 1_000_000m;
            }

            var rows = byGroup.Values
                .Select(r => new LlmUsageItem(
                    r.Name,
                    r.PromptTokens,
                    r.CompletionTokens,
                    r.Completions,
                    decimal.Round(r.InputSpend, 6, MidpointRounding.AwayFromZero),
                    decimal.Round(r.OutputSpend, 6, MidpointRounding.AwayFromZero),
                    decimal.Round(r.InputSpend + r.OutputSpend, 6, MidpointRounding.AwayFromZero)))
                .ToList();

            var descending = string.Equals(request.OrderDirection, "desc", StringComparison.OrdinalIgnoreCase);
            rows = (request.OrderBy?.ToLowerInvariant()) switch
            {
                "prompt_tokens" => descending ? rows.OrderByDescending(o => o.PromptTokens).ToList() : rows.OrderBy(o => o.PromptTokens).ToList(),
                "completion_tokens" => descending ? rows.OrderByDescending(o => o.CompletionTokens).ToList() : rows.OrderBy(o => o.CompletionTokens).ToList(),
                "input_spend" => descending ? rows.OrderByDescending(o => o.InputSpend).ToList() : rows.OrderBy(o => o.InputSpend).ToList(),
                "output_spend" => descending ? rows.OrderByDescending(o => o.OutputSpend).ToList() : rows.OrderBy(o => o.OutputSpend).ToList(),
                "total_spend" => descending ? rows.OrderByDescending(o => o.TotalSpend).ToList() : rows.OrderBy(o => o.TotalSpend).ToList(),
                _ => descending ? rows.OrderByDescending(o => o.Completions).ToList() : rows.OrderBy(o => o.Completions).ToList(),
            };

            _logger.LogInformation("llm.usage llmId={LlmId} groupBy={GroupBy} rows={RowCount}", request.LlmId, groupBy, rows.Count);

            return new QueryResult<LlmUsageItem>(rows);
        }

        private async Task<List<MetricSeriesTotal>> FetchSeriesSafeAsync(string metricName, IDictionary<string, string> baseLabels, GetLlmUsage request, CancellationToken cancellationToken)
        {
            var labels = new Dictionary<string, string>(baseLabels) { { "metric", metricName } };
            try
            {
                return await _mediator.Send(new GetMetricSeriesTotals(labels, request.From, request.To), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "llm.usage no series for metric={Metric} llmId={LlmId}", metricName, request.LlmId);
                return new List<MetricSeriesTotal>();
            }
        }

        private static void Accumulate(
            Dictionary<(string Group, string Host, string Model), Aggregate> buckets,
            List<MetricSeriesTotal> series,
            string groupBy,
            Action<Aggregate, double> apply)
        {
            foreach (var s in series)
            {
                var group = s.Labels.TryGetValue(groupBy, out var g) ? g ?? string.Empty : string.Empty;
                var host = s.Labels.TryGetValue("destination_host", out var h) ? h ?? string.Empty : string.Empty;
                var model = s.Labels.TryGetValue("destination_model", out var m) ? m ?? string.Empty : string.Empty;
                var key = (group, host, model);
                if (!buckets.TryGetValue(key, out var agg))
                {
                    agg = new Aggregate();
                    buckets[key] = agg;
                }
                apply(agg, s.Total);
            }
        }

        private static void AddIfPresent(IDictionary<string, string> labels, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                labels[key] = value;
            }
        }

        private static string TryGetHost(string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                return parsed.Host;
            }
            // Internal destinations are stored as "internal://<app>/v1"; treat the authority as host.
            var idx = uri.IndexOf("://", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var rest = uri[(idx + 3)..];
                var slash = rest.IndexOf('/');
                return slash >= 0 ? rest[..slash] : rest;
            }
            return uri;
        }

        private sealed class Aggregate
        {
            public double PromptTokens;
            public double CompletionTokens;
            public double Completions;
        }

        private sealed class LlmUsageRow
        {
            public string Name { get; set; } = string.Empty;
            public long PromptTokens;
            public long CompletionTokens;
            public long Completions;
            public decimal InputSpend;
            public decimal OutputSpend;
        }
    }
}
