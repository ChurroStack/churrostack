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
        /// the minute-floored UTC bucket of the later sample.
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

                    // Accumulate per-pair counter increases into 1-minute UTC buckets.
                    var buckets = new Dictionary<DateTimeOffset, double>();
                    for (var i = 1; i < samples.Count; i++)
                    {
                        var prev = samples[i - 1];
                        var curr = samples[i];

                        var delta = curr.Value - prev.Value;
                        // Negative delta => counter reset; the increase since the reset is the current value.
                        var raw = delta >= 0 ? delta : curr.Value;
                        // Counters are monotonically non-decreasing; treat any negative residue
                        // (floating-point drift, malformed sample, or a counter reset to a negative
                        // value) as 0 so it cannot propagate into downstream sums and surface as a
                        // negative spend in the UI.
                        var increase = raw >= 0 ? raw : 0d;

                        var ts = curr.Timestamp.UtcDateTime;
                        var bucket = new DateTimeOffset(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, 0, TimeSpan.Zero);
                        buckets[bucket] = buckets.TryGetValue(bucket, out var acc) ? acc + increase : increase;
                    }

                    return buckets.Select(kv => new MetricValueEntry(group.Key, kv.Key, kv.Value));
                })
                .ToList();
        }

        /// <summary>
        /// Buckets samples into evenly-sized intervals aligned to the user's local TZ, so a "daily" bucket
        /// represents one local day (DST-aware via calendar additions). Empty buckets are filled with 0.
        /// Each bucket start is returned as a UTC <see cref="DateTimeOffset"/>; the frontend renders the
        /// label in browser-local time.
        /// </summary>
        internal static List<MetricValueItem> AdjustOverTime(
            this List<MetricValueEntry> metricValues,
            MetricType metricType,
            DateTimeOffset start,
            DateTimeOffset end,
            TimeSpan bucketSize,
            TimeZoneInfo tz)
        {
            var localStart = TimeZoneInfo.ConvertTime(start, tz).DateTime;
            var localEnd = TimeZoneInfo.ConvertTime(end, tz).DateTime;
            var nowUtc = DateTimeOffset.UtcNow;

            // Align the first bucket to the natural boundary (midnight / top-of-hour / top-of-minute) so
            // a window like [20:43:12.604 .. 21:43:12.604] does not produce buckets at :43:12.604.
            DateTime alignedLocalStart;
            if (bucketSize >= TimeSpan.FromDays(1))
                alignedLocalStart = localStart.Date;
            else if (bucketSize >= TimeSpan.FromHours(1))
                alignedLocalStart = new DateTime(localStart.Year, localStart.Month, localStart.Day, localStart.Hour, 0, 0);
            else
                alignedLocalStart = new DateTime(localStart.Year, localStart.Month, localStart.Day, localStart.Hour, localStart.Minute, 0);

            // Walk forward in local time using calendar additions so DST transitions are honored
            // (a spring-forward day is naturally 23h; fall-back day is 25h).
            // Half-open interval [start, end): a bucket whose start equals localEnd would represent
            // the *next* window and contains no data, so it is excluded.
            var localBucketStarts = new List<DateTime>();
            for (var t = alignedLocalStart; t < localEnd; t = AddBucket(t, bucketSize))
                localBucketStarts.Add(t);

            // Convert each local bucket start to a UTC instant via the tz's offset at that local time.
            var bucketStartsUtc = localBucketStarts
                .Select(local => new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime())
                .ToList();

            // Index samples into buckets by binary-searching the UTC bucket starts.
            var perBucketPerSeries = new Dictionary<(int Bucket, long MetricId), List<double>>();
            foreach (var entry in metricValues)
            {
                var idx = FindBucketIndex(bucketStartsUtc, entry.Timestamp);
                if (idx < 0) continue; // Samples preceding the first bucket (e.g. the -5 min Rate lookback) are not rendered.
                var key = (idx, entry.MetricId);
                if (!perBucketPerSeries.TryGetValue(key, out var bag))
                    perBucketPerSeries[key] = bag = new List<double>();
                bag.Add(entry.Value);
            }

            // Aggregate within bucket per series (counter sum / gauge average) then sum across series.
            var bucketTotals = new double[bucketStartsUtc.Count];
            foreach (var ((bucket, _), values) in perBucketPerSeries)
            {
                bucketTotals[bucket] += metricType == MetricType.Counter
                    ? values.Sum()
                    : values.Average();
            }

            return bucketStartsUtc
                .Select((utc, i) => new MetricValueItem(utc, bucketTotals[i]))
                .Where(o => o.Timestamp < nowUtc)
                .ToList();
        }

        /// <summary>
        /// Bins already-Rate()'d per-minute entries into tz-aligned display buckets, aggregating by
        /// MAX (peak). First sums all series' values for each 1-minute UTC bucket, then takes the
        /// maximum of those per-minute totals within each display bucket. Empty buckets are 0.
        /// </summary>
        internal static List<MetricValueItem> AdjustPeakOverTime(
            this List<MetricValueEntry> metricValues,
            DateTimeOffset start,
            DateTimeOffset end,
            TimeSpan bucketSize,
            TimeZoneInfo tz)
        {
            var localStart = TimeZoneInfo.ConvertTime(start, tz).DateTime;
            var localEnd = TimeZoneInfo.ConvertTime(end, tz).DateTime;
            var nowUtc = DateTimeOffset.UtcNow;

            DateTime alignedLocalStart;
            if (bucketSize >= TimeSpan.FromDays(1))
                alignedLocalStart = localStart.Date;
            else if (bucketSize >= TimeSpan.FromHours(1))
                alignedLocalStart = new DateTime(localStart.Year, localStart.Month, localStart.Day, localStart.Hour, 0, 0);
            else
                alignedLocalStart = new DateTime(localStart.Year, localStart.Month, localStart.Day, localStart.Hour, localStart.Minute, 0);

            var localBucketStarts = new List<DateTime>();
            for (var t = alignedLocalStart; t < localEnd; t = AddBucket(t, bucketSize))
                localBucketStarts.Add(t);

            var bucketStartsUtc = localBucketStarts
                .Select(local => new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime())
                .ToList();

            // Sum all series per 1-minute UTC bucket (Rate() already buckets by minute per MetricId).
            var perMinuteTotal = new Dictionary<DateTimeOffset, double>();
            foreach (var entry in metricValues)
            {
                perMinuteTotal[entry.Timestamp] = perMinuteTotal.TryGetValue(entry.Timestamp, out var acc)
                    ? acc + entry.Value
                    : entry.Value;
            }

            // Bin per-minute totals into display buckets by MAX.
            var bucketPeaks = new double[bucketStartsUtc.Count];
            foreach (var (minute, total) in perMinuteTotal)
            {
                var idx = FindBucketIndex(bucketStartsUtc, minute);
                if (idx < 0) continue;
                if (total > bucketPeaks[idx])
                    bucketPeaks[idx] = total;
            }

            return bucketStartsUtc
                .Select((utc, i) => new MetricValueItem(utc, bucketPeaks[i]))
                .Where(o => o.Timestamp < nowUtc)
                .ToList();
        }

        private static DateTime AddBucket(DateTime t, TimeSpan bucketSize)
        {
            if (bucketSize >= TimeSpan.FromDays(1)) return t.AddDays(1);
            if (bucketSize >= TimeSpan.FromHours(1)) return t.AddHours(1);
            return t.AddMinutes(1);
        }

        /// <summary>Returns the largest <c>i</c> such that <c>sortedStarts[i] &lt;= ts</c>, or -1 if <c>ts</c> precedes the first bucket.</summary>
        private static int FindBucketIndex(List<DateTimeOffset> sortedStarts, DateTimeOffset ts)
        {
            if (sortedStarts.Count == 0 || ts < sortedStarts[0]) return -1;

            var lo = 0;
            var hi = sortedStarts.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (sortedStarts[mid] <= ts) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }
    }
}
