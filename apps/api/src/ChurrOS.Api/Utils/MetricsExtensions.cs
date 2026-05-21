using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Metrics;

namespace ChurrOS.Api.Utils
{
    public static class MetricsExtensions
    {
        internal static List<MetricValueEntry> Rate(this List<MetricValueEntry> values, DateTimeOffset from)
        {
            return values
                .GroupBy(o => o.MetricId)
                .SelectMany(o =>
                {
                    if (o.Count() <= 1)
                        return o.ToList(); // Insufficient data points to calculate rate

                    var metricValues = o.OrderBy(m => m.Timestamp).ToList();

                    var bucketIdx = 0;
                    var fixStart = metricValues[0].Timestamp >= from;
                    var start = metricValues[0].Timestamp.ToLocalTime();
                    var bucketStart = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, start.Offset);
                    var bucketEnd = bucketStart.AddMinutes(1);
                    var result = new List<MetricValueEntry>();

                    for (var i = 0; i < metricValues.Count; i++)
                    {
                        var metric = metricValues[i];
                        var timestamp = metricValues[i].Timestamp;
                        if (timestamp < bucketStart || timestamp >= bucketEnd)
                        {
                            double value = 0;
                            if (bucketIdx < i)
                            {
                                var values = new List<double>();
                                for (var j = bucketIdx; j < i; j++)
                                {
                                    var prev = metricValues[j];
                                    var curr = metricValues[j + 1];
                                    var delta = curr.Value - prev.Value;
                                    values.Add(delta >= 0 ? delta : curr.Value);
                                }
                                value = values.Sum();
                            }
                            if (result.Count > 0)
                            {
                                var lastAdded = result[result.Count - 1];
                                if ((bucketStart - lastAdded.Timestamp).TotalMinutes > 2)
                                {
                                    // TODO: Fix spikes
                                    value += metric.Value;
                                }
                            }
                            if (fixStart)
                            {
                                value += metricValues[0].Value;
                                fixStart = false;
                            }
                            result.Add(new MetricValueEntry(metric.MetricId, bucketStart, value));
                            bucketIdx = i;
                            bucketStart = metric.Timestamp;
                            bucketEnd = bucketStart.AddMinutes(1);
                        }
                    }

                    return result;
                })
                .ToList();
        }

        internal static List<MetricValueItem> AdjustOverTime(this List<MetricValueEntry> metricValues, MetricType metricType, DateTimeOffset start, DateTimeOffset end, string aggregateBy = "yyyyMMddHHmm", bool average = false)
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
                        Value = average ? metricGroup.Average(x => x.Value) : (metricType == MetricType.Counter ? metricGroup.Sum(x => x.Value) : metricGroup.Average(x => x.Value))
                    })
                )
                .GroupBy(x => x.Date)
                .Select(metricGroup => new
                {
                    Date = metricGroup.Key,
                    Value = average ? metricGroup.Average(x => x.Value) : metricGroup.Sum(x => x.Value)
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
