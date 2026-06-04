using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using System.Collections.Concurrent;
using System.Net;

namespace ChurrOS.Api.Services
{
    public class MetricsAggregatorService
    {
        internal record MetricInfo(long MetricId, long AccountId, MetricType? Type, IDictionary<string, string> Labels, double Value, DateTimeOffset LastUpdatedUtc);

        // Gauges (cpu/memory/...) are instantaneous: a series only describes reality while its pod is live.
        // Series are keyed by the ephemeral pod name, so once a pod dies its entry would otherwise linger and
        // be re-emitted on every ScrapeMetrics() forever, inflating charts (see postmortem 2026-06-04). We
        // drop a gauge entry that has not been refreshed within this window so dead pods stop being replayed.
        // Sized at 3x the 30s metrics scrape interval (UpdateEnvironmentJobs.Handler.ScheduleMetricsScrappingJob)
        // so a single missed/failed scrape never prunes a still-live pod. Counters must NOT be pruned: they are
        // accumulated and re-emitted so MetricsExtensions.Rate() can diff consecutive cumulative samples.
        private static readonly TimeSpan GaugeStaleWindow = TimeSpan.FromSeconds(90);

        private Dictionary<string, MetricInfo> _metrics { get; } = new Dictionary<string, MetricInfo>();
        // Thread-safe so AddMetric's fast-path read and ScrapeMetrics' pruning removal stay lock-free and
        // race-free; the lock below only serializes the once-per-key DB resolve, not dictionary access.
        private ConcurrentDictionary<string, long> _metricKeyToIdCache { get; } = new ConcurrentDictionary<string, long>();
        private readonly object _metricIdResolutionLock = new object();
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IIdGeneratorService _idGeneratorService;

        public MetricsAggregatorService(IServiceScopeFactory serviceScopeFactory, IIdGeneratorService idGeneratorService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _idGeneratorService = idGeneratorService;
        }

        public void AddMetric(MetricItem metric)
        {
            if (!metric.Labels.TryAdd("source", Dns.GetHostName()))
            {
                metric.Labels["source"] = Dns.GetHostName();
            }
            var keyItem = GetKeyFromItem(metric);
            var key = keyItem.Key;
            if (!_metricKeyToIdCache.TryGetValue(key, out var metricId))
            {
                lock (_metricIdResolutionLock)
                {
                    if (!_metricKeyToIdCache.TryGetValue(key, out metricId))
                    {
                        using var scope = _serviceScopeFactory.CreateScope();
                        var tenantResolver = scope.ServiceProvider.GetRequiredService<ITenantResolver>();
                        tenantResolver.SetAccountId(metric.AccountId);
                        var dbContext = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
                        var existingMetric = dbContext.Set<Domain.Metric>().Find(metric.AccountId, keyItem.Hash);
                        if (existingMetric is null)
                        {
                            metricId = _idGeneratorService.CreateLongId();
                            dbContext.Set<Domain.Metric>().Add(new Domain.Metric(metric.AccountId, keyItem.Hash, metricId, metric.Type ?? Models.Dtos.MetricType.Counter, metric.Labels));
                            dbContext.SaveChanges();
                        }
                        else
                        {
                            metricId = existingMetric.MetricId;
                        }
                    }
                    _metricKeyToIdCache[key] = metricId;
                }
            }
            var nowUtc = DateTimeOffset.UtcNow;
            lock (_metrics)
            {
                if (_metrics.TryGetValue(key, out var existingMetric))
                {
                    var value = (existingMetric.Type == MetricType.Counter) ? existingMetric.Value + metric.Value : metric.Value;
                    _metrics[key] = new MetricInfo(existingMetric.MetricId, existingMetric.AccountId, existingMetric.Type, existingMetric.Labels, value, nowUtc);
                }
                else
                {
                    _metrics.Add(key, new MetricInfo(metricId, metric.AccountId, metric.Type, metric.Labels, metric.Value, nowUtc));
                }
            }
        }

        public (long MetricId, MetricItem Metric)[] ScrapeMetrics()
        {
            var now = DateTimeOffset.UtcNow;

            (long MetricId, MetricItem Metric)[] snapshot;
            HashSet<string> staleGaugeKeys;
            lock (_metrics)
            {
                // A gauge series that hasn't been refreshed within GaugeStaleWindow belongs to a pod that
                // stopped reporting; drop it so it is no longer replayed. Counters are always retained.
                staleGaugeKeys = _metrics
                    .Where(o => o.Value.Type == MetricType.Gauge && now - o.Value.LastUpdatedUtc > GaugeStaleWindow)
                    .Select(o => o.Key)
                    .ToHashSet();

                // Emit everything except the stale gauges (so we never write a known-dead pod's frozen value),
                // stamped with a single 'now' so all rows from this scrape share a timestamp.
                snapshot = _metrics
                    .Where(o => !staleGaugeKeys.Contains(o.Key))
                    .Select(o => (o.Value.MetricId, new MetricItem(o.Value.AccountId, o.Value.Labels, now, o.Value.Value, o.Value.Type)))
                    .ToArray();

                foreach (var staleKey in staleGaugeKeys)
                {
                    _metrics.Remove(staleKey);
                }
            }

            // Drop the id-cache entries for pruned series too, keeping the cache bounded. ConcurrentDictionary
            // makes each removal atomic against AddMetric's reads/writes; if the same key ever reappears,
            // AddMetric re-resolves the id from the existing Metric row.
            foreach (var staleKey in staleGaugeKeys)
            {
                _metricKeyToIdCache.TryRemove(staleKey, out _);
            }

            return snapshot;
        }

        public static (string Key, byte[] Hash) GetKeyFromItem(MetricItem item)
        {
            var parts = new List<string>()
            {
                item.AccountId.ToString()
            };
            foreach (var label in item.Labels ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                parts.Add($"{label.Key?.ToLowerInvariant() ?? ""}={label.Value?.ToLowerInvariant()}");
            }
            var hash = string.Join(":", parts).GetSha1Hash();
            return (Convert.ToHexString(hash), hash);
        }
    }
}
