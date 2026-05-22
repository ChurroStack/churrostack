# Fix metrics calculation — CPU metric type, Rate(), counter totals buffer

**Date:** 2026-05-22
**Area:** `apps/api` metrics pipeline (Prometheus mimic), `apps/ui` charts

## Trigger

Review of how ChurroStack mimics Prometheus in C#/.NET — verifying that the
CPU, memory and traffic metrics behind the application and LLM charts
(`apps/ui/src/pages/applications/charts`, `apps/ui/src/pages/llms/charts`) are
calculated correctly.

## Root cause

The runner (`apps/churrun-kubernetes`, `KubernetesService.ScrapeMetricsAsync`)
reads the Kubernetes Metrics API, which reports **CPU as an instantaneous
value in cores** — the same shape as memory. The API nevertheless ingested
`cpu_usage` (and `gpu_usage`) as a Prometheus **Counter**:

- `MetricsAggregatorService` accumulates counter samples, turning the
  instantaneous CPU readings into an ever-growing sum.
- `GetMetrics` then ran `Rate()` to recover the per-sample deltas, and a
  `doAverage` special-case averaged them back to an instantaneous value.

This accumulate → rate → average round-trip was accidental complexity, and it
routed CPU through `Rate()`, which had several independent bugs:

1. The final (most recent) minute bucket was never emitted — the loop only
   closed a bucket when a later-bucket sample arrived.
2. Gap handling (`// TODO: Fix spikes`) added the absolute cumulative counter
   value on >2-minute gaps, producing huge artificial spikes.
3. `fixStart` added the absolute counter value into the first bucket when no
   lookback sample existed — another spike.
4. Single-sample series returned the raw cumulative value as if it were a rate.
5. Bucket windows drifted off minute alignment after the first bucket.

Separately, `GetMetricTotals` computed a 5-minute lookback (`dateFrom`) but
filtered the query on `from`, so counter totals had no predecessor sample for
the first bucket and were inflated.

Two cosmetic UI issues: the CPU chart was labelled "millicores" although the
data (and the `parseCpu`-derived Y-axis max) are in cores, and the bytes-out
chart's error alert said "bytes in chart".

Memory and storage (`Gauge`) were calculated correctly and were not changed.

## Fix

- `ScrapeMetricsJob.cs` — ingest `cpu_usage` and `gpu_usage` as
  `MetricType.Gauge` (instantaneous readings), like `memory_usage`.
- `MetricsExtensions.cs` — rewrote `Rate()` with correct Prometheus
  `increase()` semantics: per series, diff consecutive samples (negative delta
  = counter reset → increase is the current value), sum each increase into the
  minute-floored bucket of the later sample, emit one entry per non-empty
  bucket, return empty for single-sample series. Removed the now-unused
  `average` parameter / `doAverage` branch from `AdjustOverTime`.
- `GetMetrics.Handler.cs` — removed the `cpu_usage` `doAverage` special-case;
  CPU now follows the gauge path.
- `GetMetricTotals.Handler.cs` — query now filters on `dateFrom` (the 5-minute
  lookback) so the counter `Rate()` has a predecessor sample.
- `cpu-usage.tsx` — chart relabelled "Cpu Usage (cores)".
- `bytes-out.tsx` — error alert text corrected to "bytes out chart".

### Data migration

`Metric.Type` is persisted per series, so existing `cs.metric` rows for
`cpu_usage`/`gpu_usage` would keep `Counter` until updated. This is handled
automatically by the EF Core migration
`20260522083851_RetypeCpuGpuMetricsAsGauge` — `Program.cs` runs
`context.Database.Migrate()` on every boot, so the data fix is applied on the
first deploy with no manual step. The migration:

- retypes `cpu_usage`/`gpu_usage` series to `Gauge`, and
- purges their accumulated `metric_value` history so old CPU charts do not
  render a monotonic ramp.

A deploy restarts the API process, clearing the in-memory aggregator; the
migration and restart are part of the same boot, so ordering is not a concern.

## Out of scope (follow-up)

`MetricsAggregatorService` is a global singleton and `ScrapeMetrics()` dumps
the entire dictionary on every per-environment `ScrapeMetricsJob` run, so each
series is written once per environment per scrape. This is storage bloat /
redundant writes; it does not corrupt calculations (counter rate telescopes,
gauges only smear timestamps slightly). A per-environment scrape/flush is a
separate change.

## Verification

- `pnpm nx build api` and `pnpm nx build ui` both succeed.
- `Rate()` reasoning check: counter samples `100, 150, 150, 400` across three
  minutes produce buckets `50, 0, 250` — no leading/trailing absolute-value
  spike, and the most recent minute is present. A single-sample series yields
  an empty result.
- CPU end to end against a live environment: `/api/applications/{name}/metrics/cpu_usage`
  returns instantaneous-scale core values (no monotonic ramp, no post-gap
  spikes) and the latest minute bucket is populated.
- `GetMetricTotals` for a counter no longer shows an inflated first data point.
- UI: the CPU card reads "Cpu Usage (cores)"; the bytes-out error alert reads
  "Error loading bytes out chart".
