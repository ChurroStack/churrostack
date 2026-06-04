# Fix fake/stale CPU & memory charts — gauge series replayed forever

**Date:** 2026-06-04
**Area:** `apps/api` metrics pipeline (`MetricsAggregatorService`), application
CPU/memory charts (`apps/ui/src/pages/applications/charts`)

## Trigger

Application `invoice-scanner` showed high CPU and memory on its charts that did
not match Kubernetes (`kubectl top`), and the charts kept showing high values
**even after the app was stopped**.

## Root cause

`MetricsAggregatorService` is registered as a **singleton**
(`Program.cs`) and keeps every metric it has ever seen in a process-lifetime
dictionary `_metrics`, keyed by the full label set — which **includes the
ephemeral pod name** (`target = <podname>:<container>`). Pod names change on
every restart/redeploy, so each pod generation creates a brand-new set of
series.

`ScrapeMetrics()` returned **all** entries stamped with `DateTimeOffset.UtcNow`
and **never cleared the dictionary**. `ScrapeMetricsJob` (one per environment,
every 30 s) adds the current live-pod gauges, then drains the whole buffer into
the `cs.metric_value` hypertable via a binary `COPY`.

Consequence: once a pod's gauge was recorded, its **last value was re-written to
the DB on every scrape cycle, forever**, with a fresh timestamp — for every pod
that had ever existed since the API process started.

Because the chart query (`GetApplicationMetrics` → `GetMetrics` →
`MetricsBucketService.AdjustOverTime`) filters series only by `application_id` +
`metric` and **sums per-series gauge averages across all matching series**, the
chart summed the frozen last values of every dead pod.

### Evidence (from the live DB, app id `1492093143108026368`, `mode=0` / `replicas=1` → should be ONE pod)

- **30 `cpu_usage` + 30 `memory_usage` series** across **3 replicaset
  generations** (`df5d6d477`, `6f7b44bdb4`, `7c7fc5bcf9`).
- The latest scrape wrote **28 containers / 14 pods simultaneously**: CPU summed
  to **~16.2 cores** (`Application.Size` = 8) and memory to **~45 GiB** (size =
  32Gi).
- Per-pod values were **bit-identical across 8 consecutive scrapes over ~37 s**
  (e.g. `…-r7g9j:streamlit = 3.07920` every cycle). Live metrics-server readings
  never repeat to 5 decimals — proof the rows were **replays of a frozen
  snapshot**, not live readings. A pod from a May-27 replicaset was still
  "reporting" on Jun-4.

This explains every symptom: **fake-high** (sum of many ghost pods), **doesn't
match Kubernetes** (`kubectl top` shows only live pods), and **stays high after
stop** (the buffer replays the last values regardless of real pod state).

This is the same service touched by the prior
[`2026-05-22-fix-metrics-calculation`](../2026-05-22-fix-metrics-calculation/README.md)
postmortem (which correctly retyped `cpu_usage`/`gpu_usage` as `Gauge`); that
retype is what makes the targeted gauge-only fix below possible.

## Fix

`MetricsAggregatorService.cs` — make the gauge buffer self-expiring instead of
unbounded:

- Added `LastUpdatedUtc` to the internal `MetricInfo` record; `AddMetric`
  stamps it on both the insert and update branches.
- `ScrapeMetrics()` now, under `lock(_metrics)`, builds the emit snapshot
  **excluding** gauge series not refreshed within `GaugeStaleWindow` (90 s =
  3× the 30 s scrape interval, so a single missed/failed scrape never prunes a
  still-live pod), then removes those stale gauge entries. Their keys are also
  dropped from `_metricKeyToIdCache` (outside the lock, never nested, to
  preserve `AddMetric`'s lock ordering).
- **Counters are never pruned**: `bytes_in`/`bytes_out` and LLM token metrics
  are accumulated and re-emitted so `MetricsExtensions.Rate()` can diff
  consecutive cumulative samples. Clearing them would break the HTTP/LLM rate
  charts.

The DB `COPY` stays in `ScrapeMetricsJob`, outside the lock — the critical
section is only the in-memory snapshot + mutation.

### Why the fix is write-side

The stale rows carry **current** timestamps, so no read-side recency/liveness
filter in `MetricsBucketService` can distinguish them from real data. The buffer
had to stop replaying stale gauges at the source.

## Verification

1. `pnpm nx build api` (the project builds; 0 errors).
2. Run the API so the singleton resets and scrapes resume. Then, against
   `cs.metric_value`:
   - The latest scrape writes **~2** `cpu_usage` series for `invoice-scanner`
     (one pod × two containers), **not 28**, and only one replicaset generation
     reports.
   - After stopping the app, **no new** `cpu_usage`/`memory_usage` rows appear
     for it within ~90 s.
3. The chart total matches `kubectl top pod` for the live pod(s).
4. Regression: `bytes_in`/`bytes_out` (counter) charts are unchanged — the
   `Rate()` path is untouched.

## Follow-ups (out of scope for this change)

- **Historical data:** `cs.metric_value` already holds weeks of replayed gauge
  rows for every app. This fix only corrects data going forward; past time
  ranges stay inflated until they age out of the viewed window. A targeted
  purge of dead-pod gauge rows (dry-run counted first) can clean them up.
- **Confirm the pod count in-cluster:** if `kubectl get pods -n <ns>` shows real
  pods from multiple replicaset generations alive at once, there is a *separate*
  orphaned-pod / rollout-cleanup bug in the runner that this change does not
  address.
- **Counter N× duplication:** because the buffer is global but each environment
  runs its own `ScrapeMetricsJob`, counters are re-emitted once per environment
  per cycle. `Rate()` diffs by `metric_id`, so values are not inflated, but the
  rows are redundant — a candidate for a later per-environment drain.
