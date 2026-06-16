# Add Peak RPM / TPM charts and columns to LLMs Monitoring

**Date:** 2026-06-16

## Trigger

Users wanted throughput visibility beyond cumulative totals: how many requests/tokens
per minute at peak, both as time-series charts and as sortable table columns per identity.

## Root Cause / Background

No peak-per-minute data existed in the API. The existing metrics pipeline stored
cumulative counters flushed every 30 s and exposed only bucketed-sum series and
per-window totals. There was no way to compute max-per-minute without backend changes.

## Fix

### Backend

- **`GetMetricSeriesPerMinute` command** — mirrors `GetMetricSeriesTotals` but returns
  per-series per-minute rows (Rate()'d, lookback filtered so pre-window minutes can't
  become spurious peaks).
- **`LlmUsageItem.PeakRpm / PeakTpm`** — new DTO fields (default 0; camelCase JSON).
- **`LlmUsageAggregator.ComputePeaks`** — groups per-minute series by (groupBy, minute),
  sums across series, takes MAX; invariant: peak = tallest bar of the 1-minute chart.
- **`MetricsExtensions.AdjustPeakOverTime`** — like `AdjustOverTime` but aggregates
  display buckets by MAX instead of SUM; reuses the same bucket-boundary walk.
- **`IMetricsBucketService` + `MetricsBucketService.BuildPeakPerMinuteSeriesAsync`** —
  new method: Rate() → filter lookback → AdjustPeakOverTime.
- **`GetAggregatedLlmMetrics` / `GetLlmMetrics` handlers** — intercept synthetic metric
  names (`requests_per_minute`, `tokens_per_minute`), fetch underlying stored metrics,
  combine, call `BuildPeakPerMinuteSeriesAsync`. Existing metric names unchanged.
- **`GetAggregatedLlmUsage` / `GetLlmUsage` handlers** — add parallel per-minute fetches
  for the three stored metrics, call `ComputePeaks`, assign PeakRpm/PeakTpm per row.
  `BuildRows` / totals / spend path byte-identical.

### Frontend

- `LlmUsageSummaryItem` — added `peakRpm: number; peakTpm: number`.
- `usage-summary.tsx` — two new sortable columns (Peak RPM, Peak TPM) after
  Completion Tokens, before Input Spend. Cell coerces `?? 0` to prevent NaN on null.
- `aggregated-monitor-panel.tsx` / `monitor-panel.tsx` — two new `<TokensUsageChart>`
  with `metricName="requests_per_minute"` and `metricName="tokens_per_minute"`.

## Verification

1. `pnpm nx build api` — 0 errors.
2. `pnpm run build` in `apps/ui` — 0 errors.
3. Manual: GET `/api/llms/metrics/requests_per_minute` and `.../tokens_per_minute`
   return bucketed `{timestamp,value}`; ≤1h → per-minute bars, 24h → hourly peak.
4. Table Peak RPM/TPM columns populate, sort, and coerce 0 (not NaN) for new identities.
5. Regression gate: existing token charts, KPI totals, and spend are numerically unchanged.
