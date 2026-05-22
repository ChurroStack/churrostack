using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;

namespace ChurrOS.Api.Utils
{
    public static class MetricsExtensions
    {
        /// <summary>
        /// Computes the per-minute increase of a counter series, mimicking Prometheus <c>increase()</c>.
        /// For each series, consecutive samples are diffed; a negative delta is treated as a counter
        /// reset (the increase since the reset is the current value). Each increase is accumulated into
        /// the minute-floored bucket of the later sample.
        /// </summary>
        internal static List<MetricValueEntry> Rate(this List<MetricValueEntry> values)
        {
            return values
                .GroupBy(o => o.MetricId)
                .SelectMany(group =>
                {
                    var samples = group.OrderBy(m => m.Timestamp).ToList();
                    if (samples.Count <= 1)
                        return Enumerable.Empty<MetricValueEntry>(); // Insufficient data points to calculate rate

                    // Accumulate per-pair counter increases into 1-minute buckets.
                    var buckets = new Dictionary<DateTimeOffset, double>();
                    for (var i = 1; i < samples.Count; i++)
                    {
                        var prev = samples[i - 1];
                        var curr = samples[i];

                        var delta = curr.Value - prev.Value;
                        // Negative delta => counter reset; the increase since the reset is the current value.
                        var increase = delta >= 0 ? delta : curr.Value;

                        var ts = curr.Timestamp.ToLocalTime();
                        var bucket = new DateTimeOffset(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, 0, ts.Offset);
                        buckets[bucket] = buckets.TryGetValue(bucket, out var acc) ? acc + increase : increase;
                    }

                    return buckets.Select(kv => new MetricValueEntry(group.Key, kv.Key, kv.Value));
                })
                .ToList();
        }

        internal static List<MetricValueItem> AdjustOverTime(this List<MetricValueEntry> metricValues, MetricType metricType, DateTimeOffset start, DateTimeOffset end, string aggregateBy = "yyyyMMddHHmm")
        {
            // Get time range in local timezone
            var from = start.LocalDateTime;
            var to = end.LocalDateTime;
            var now = DateTime.Now;

            var sum = metricValues
                .GroupBy(m => m.Timestamp.LocalDateTime.ToString(aggregateBy))
                .SelectMany(dateGroup => dateGroup
                    .GroupBy(x => x.MetricId)
                    .Select(metricGroup => new
                    {
                        MetricId = metricGroup.Key,
                        Date = dateGroup.Key,
                        // Counters sum their per-minute increases over the bucket; gauges average their readings.
                        Value = metricType == MetricType.Counter ? metricGroup.Sum(x => x.Value) : metricGroup.Average(x => x.Value)
                    })
                )
                .GroupBy(x => x.Date)
                // Across series within a bucket: sum (total across deployments) for both counters and gauges.
                .Select(metricGroup => new
                {
                    Date = metricGroup.Key,
                    Value = metricGroup.Sum(x => x.Value)
                })
                .ToDictionary(g => g.Date, g => g.Value);

            // Fill missing time buckets with zero values
            switch (aggregateBy)
            {
                case "yyyyMMdd":
                    return Enumerable
                        .Range(0, (int)(to - from).TotalDays + 1)
                        .Select(i =>
                        {
                            var day = from.AddDays(i);
                            return new MetricValueItem(
                                day,
                                sum.TryGetValue(day.ToString(aggregateBy), out var value) ? value : 0
                            );
                        })
                        .Where(o => o.Timestamp < now)
                        .ToList();
                case "yyyyMMddHH":
                    return Enumerable
                        .Range(0, (int)(to - from).TotalHours + 1)
                        .Select(i =>
                        {
                            var hour = from.AddHours(i);
                            return new MetricValueItem(
                                hour,
                                sum.TryGetValue(hour.ToString(aggregateBy), out var value) ? value : 0
                            );
                        })
                        .Where(o => o.Timestamp < now)
                        .ToList();
                case "yyyyMMddHHmm":
                default:
                    return Enumerable
                        .Range(0, (int)(to - from).TotalMinutes + 1)
                        .Select(i =>
                        {
                            var minute = from.AddMinutes(i);
                            return new MetricValueItem(
                                minute,
                                sum.TryGetValue(minute.ToString(aggregateBy), out var value) ? value : 0
                            );
                        })
                        .Where(o => o.Timestamp < now)
                        .ToList();
            }
        }
    }
}
