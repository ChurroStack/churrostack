using ChurrOS.Api.Commands.Metrics;
using ChurrOS.Api.Models.Dtos.Llm;

namespace ChurrOS.Api.Services
{
    /// <summary>
    /// Shared accumulation + pricing + spend-clamp pipeline used by both <c>GetLlmUsageHandler</c>
    /// (single LLM) and <c>GetAggregatedLlmUsageHandler</c> (cross-LLM). Stateless utility so the
    /// price-merge semantics and spend-rounding stay in one place.
    /// </summary>
    public static class LlmUsageAggregator
    {
        /// <summary>
        /// Bucket the three metric series (prompt_tokens / completion_tokens / completion_count) by
        /// <paramref name="groupBy"/> label + (destination_host, destination_model), apply per-(host,
        /// model) pricing, clamp spend to non-negative, round to 6 decimal places, and return one row
        /// per group value.
        /// </summary>
        public static List<LlmUsageItem> BuildRows(
            string groupBy,
            List<MetricSeriesTotal> promptSeries,
            List<MetricSeriesTotal> completionSeries,
            List<MetricSeriesTotal> countSeries,
            Dictionary<(string Host, string Model), (decimal InPer1M, decimal OutPer1M)> priceByHostModel,
            Dictionary<string, (decimal InPer1M, decimal OutPer1M)> priceByModel)
        {
            // Accumulator: (groupValue, host, model) -> tokens / completions.
            var buckets = new Dictionary<(string Group, string Host, string Model), Aggregate>();
            Accumulate(buckets, promptSeries, groupBy, (a, v) => a.PromptTokens += v);
            Accumulate(buckets, completionSeries, groupBy, (a, v) => a.CompletionTokens += v);
            Accumulate(buckets, countSeries, groupBy, (a, v) => a.Completions += v);

            // Collapse to one row per group value, applying per-(host,model) prices. priceByModel is a
            // fallback for metrics whose destination_host label doesn't match any declared URI host.
            var byGroup = new Dictionary<string, UsageRow>();
            foreach (var ((group, host, model), agg) in buckets)
            {
                if (!byGroup.TryGetValue(group, out var row))
                {
                    row = new UsageRow { Name = group };
                    byGroup[group] = row;
                }
                row.PromptTokens += (long)agg.PromptTokens;
                row.CompletionTokens += (long)agg.CompletionTokens;
                row.Completions += (long)agg.Completions;

                var price = priceByHostModel.TryGetValue((host, model), out var p) ? p
                    : priceByModel.TryGetValue(model, out var pm) ? pm
                    : (InPer1M: 0m, OutPer1M: 0m);
                row.InputSpend += ((decimal)agg.PromptTokens) * price.InPer1M / 1_000_000m;
                row.OutputSpend += ((decimal)agg.CompletionTokens) * price.OutPer1M / 1_000_000m;
            }

            return byGroup.Values
                .Select(r =>
                {
                    // Spend is always non-negative. Clamp before rounding so a tiny negative drift
                    // (e.g. floating-point residue from rate-of-counter math) cannot survive as a
                    // negative zero through Intl.NumberFormat in the UI ("-$0.00").
                    var input = r.InputSpend < 0m ? 0m : r.InputSpend;
                    var output = r.OutputSpend < 0m ? 0m : r.OutputSpend;
                    return new LlmUsageItem(
                        r.Name,
                        r.PromptTokens,
                        r.CompletionTokens,
                        r.Completions,
                        decimal.Round(input, 6, MidpointRounding.AwayFromZero),
                        decimal.Round(output, 6, MidpointRounding.AwayFromZero),
                        decimal.Round(input + output, 6, MidpointRounding.AwayFromZero));
                })
                .ToList();
        }

        /// <summary>
        /// Sort rows by the requested column / direction. Falls back to <c>completions desc</c> when
        /// the column is unrecognised.
        /// </summary>
        public static List<LlmUsageItem> Sort(List<LlmUsageItem> rows, string? orderBy, string? orderDirection)
        {
            var descending = string.Equals(orderDirection, "desc", StringComparison.OrdinalIgnoreCase);
            return (orderBy?.ToLowerInvariant()) switch
            {
                "prompt_tokens" => descending ? rows.OrderByDescending(o => o.PromptTokens).ToList() : rows.OrderBy(o => o.PromptTokens).ToList(),
                "completion_tokens" => descending ? rows.OrderByDescending(o => o.CompletionTokens).ToList() : rows.OrderBy(o => o.CompletionTokens).ToList(),
                "input_spend" => descending ? rows.OrderByDescending(o => o.InputSpend).ToList() : rows.OrderBy(o => o.InputSpend).ToList(),
                "output_spend" => descending ? rows.OrderByDescending(o => o.OutputSpend).ToList() : rows.OrderBy(o => o.OutputSpend).ToList(),
                "total_spend" => descending ? rows.OrderByDescending(o => o.TotalSpend).ToList() : rows.OrderBy(o => o.TotalSpend).ToList(),
                _ => descending ? rows.OrderByDescending(o => o.Completions).ToList() : rows.OrderBy(o => o.Completions).ToList(),
            };
        }

        /// <summary>
        /// Build the dual price maps (by-host-model first, by-model fallback) from a flat list of
        /// destinations. First-wins on collisions.
        /// </summary>
        public static (Dictionary<(string Host, string Model), (decimal InPer1M, decimal OutPer1M)> ByHostModel,
                       Dictionary<string, (decimal InPer1M, decimal OutPer1M)> ByModel)
            BuildPriceMaps(IEnumerable<LLmDestinationItem> destinations)
        {
            var priceByHostModel = new Dictionary<(string Host, string Model), (decimal InPer1M, decimal OutPer1M)>();
            var priceByModel = new Dictionary<string, (decimal InPer1M, decimal OutPer1M)>(StringComparer.Ordinal);
            foreach (var dest in destinations)
            {
                var host = TryGetHost(dest.Uri);
                var model = dest.Model ?? string.Empty;
                var price = (
                    InPer1M: dest.InputTokenPricePer1M ?? 0m,
                    OutPer1M: dest.OutputTokenPricePer1M ?? 0m);
                var key = (host, model);
                if (!priceByHostModel.ContainsKey(key))
                {
                    priceByHostModel[key] = price;
                }
                if ((price.InPer1M > 0 || price.OutPer1M > 0) && !priceByModel.ContainsKey(model))
                {
                    priceByModel[model] = price;
                }
            }
            return (priceByHostModel, priceByModel);
        }

        /// <summary>Extract the host segment from an absolute or pseudo-scheme URI (e.g. <c>internal://app/v1</c>).</summary>
        public static string TryGetHost(string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return string.Empty;
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                return parsed.Host;
            }
            var idx = uri.IndexOf("://", StringComparison.Ordinal);
            if (idx >= 0)
            {
                var rest = uri[(idx + 3)..];
                var slash = rest.IndexOf('/');
                return slash >= 0 ? rest[..slash] : rest;
            }
            return uri;
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

        private sealed class Aggregate
        {
            public double PromptTokens;
            public double CompletionTokens;
            public double Completions;
        }

        private sealed class UsageRow
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
