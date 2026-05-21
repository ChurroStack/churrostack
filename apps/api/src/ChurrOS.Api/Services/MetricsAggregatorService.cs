using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;
using ChurrOS.Api.Utils;
using System.Net;

namespace ChurrOS.Api.Services
{
    public class MetricsAggregatorService
    {
        internal record MetricInfo(long MetricId, long AccountId, MetricType? Type, IDictionary<string, string> Labels, double Value);
        private Dictionary<string, MetricInfo> _metrics { get; } = new Dictionary<string, MetricInfo>();
        private Dictionary<string, long> _metricKeyToIdCache { get; } = new Dictionary<string, long>();
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
                lock (_metricKeyToIdCache)
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
            lock (_metrics)
            {
                if (_metrics.TryGetValue(key, out var existingMetric))
                {
                    var value = (existingMetric.Type == MetricType.Counter) ? existingMetric.Value + metric.Value : metric.Value;
                    _metrics[key] = new MetricInfo(existingMetric.MetricId, existingMetric.AccountId, existingMetric.Type, existingMetric.Labels, value);
                }
                else
                {
                    _metrics.Add(key, new MetricInfo(metricId, metric.AccountId, metric.Type, metric.Labels, metric.Value));
                }
            }
        }

        public (long MetricId, MetricItem Metric)[] ScrapeMetrics()
        {
            var now = DateTimeOffset.UtcNow;
            return _metrics.Values.Select(o => (o.MetricId, new MetricItem(o.AccountId, o.Labels, now, o.Value, o.Type))).ToArray();
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
