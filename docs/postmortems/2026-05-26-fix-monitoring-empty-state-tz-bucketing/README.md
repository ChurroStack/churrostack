# Fix monitoring — empty-state UX + TZ-aware bucketing across regions

**Date:** 2026-05-26
**Area:** `apps/api` metrics pipeline + every dated `apps/api` endpoint, `apps/ui` Monitoring tab

## Trigger

User reports against the Application Monitoring tab:

1. Picking a metric the app had never recorded surfaced a red destructive "Error loading … chart" alert. The actual cause is "no data yet", not a failure.
2. With a Madrid (UTC+2) browser and only a few minutes of fresh data, the `Last hour` preset showed real bars but `Today`, `Last 7 days`, and `This Month` rendered all zeros.

Live evidence from `apps.fi-group.com`:

- `…/memory_usage?from=…20:43Z&to=…21:43Z` (Last hour) returned ~30 buckets of real memory data (~447 MB) starting at `21:09:12Z`.
- `…/memory_usage?from=2026-04-30T22:00:00Z&to=2026-05-25T21:59:59Z` (This Month, Madrid) returned 25 daily buckets, all zero, even though the same May 25 data existed.

## Root cause

Two distinct bugs, plus a latent class of bugs surfaced by an audit of every dated endpoint.

1. **Empty-state surfaced as error.** `GetMetricsHandler` threw `NotFoundException` (→ HTTP 404) whenever no `Metric` series matched the requested labels. The UI's generic `useGet` hook treats any non-OK response as `error`, so the chart components rendered a red destructive `<Alert>` instead of distinguishing "no data yet" from "real failure".

2. **Calendar-string bucket keys misaligned with the window when `from` did not land on UTC midnight.** `MetricsExtensions.AdjustOverTime` keyed both data and the fill range as `yyyyMMdd` / `yyyyMMddHH` / `yyyyMMddHHmm` strings derived from `LocalDateTime`. In a Madrid (UTC+2) browser, `from = startOfMonth(today) = 2026-05-01T00:00 Madrid` serialized to `2026-04-30T22:00:00Z`. The backend's fill loop iterated `from.AddDays(0..24).ToString("yyyyMMdd")` → `20260430 … 20260524`. A May 25 sample at `2026-05-25T21:09:12Z` grouped under `20260525`, which was **not in the fill range** — silently dropped. The same misalignment was latent for every multi-region access (Brazil, etc.), where "today" / "last 7 days" mean different UTC windows.

3. **Latent `DateTime.Today` server-local defaults** in three sibling handlers (`GetApplicationTraces`, `GetApplicationUsage`, `GetMetricTotals`). These endpoints don't bucket over time, but their defaulted window was the server's local-today, not the user's local-today.

## Fix

### Backend — empty-state

- `GetMetrics.Handler.cs` — replaced the `NotFoundException` throw with `return new MetricValuesItem(metricName, request.Labels, [])`, mirroring the existing empty-`Values` branch. Same handler also added a second empty-result `LogDebug` for the existing "no values in window" path so both empty paths are observable. Injected `ILogger<GetMetricsHandler>`.

### Backend — TZ-aware bucketing

- `GetMetrics.cs`, `GetApplicationMetrics.cs`, `GetLlmMetrics.cs` — added optional `string? Tz` (IANA name).
- `ApplicationController.cs`, `LlmController.cs` — added `[FromQuery] string? tz` to the two metrics endpoints; forwarded into the command.
- `GetMetrics.Handler.cs` — resolves `TimeZoneInfo.FindSystemTimeZoneById(request.Tz)` with a UTC fallback that logs a `LogWarning` on `TimeZoneNotFoundException` / `InvalidTimeZoneException`. Picks `bucketSize` from window length (`>1d → 1d`, `>1h → 1h`, else `1m`). Defaults `from`/`to` to `DateTime.UtcNow.Date` (no longer server-local).
- `MetricsExtensions.AdjustOverTime` — rewritten to window-anchored, TZ-aware index bucketing. The handler hands in a `TimeZoneInfo`; the function converts `start`/`end` to local time, floors `start` to the bucket boundary (`.Date` / top-of-hour / top-of-minute) to avoid sub-second bucket timestamps like `20:43:12.604`, walks forward in local time with calendar `AddDays` / `AddHours` / `AddMinutes` (so DST spring-forward is a 23h day and fall-back is 25h, automatically), and converts each local bucket start back to UTC via `tz.GetUtcOffset(local)`. The walk uses the half-open interval `[start, end)` (`t < localEnd`) so a window like `[00:00, 01:00)` never produces an empty trailing bucket. Samples are placed into buckets via binary search on the UTC start list; per-bucket per-series aggregation is `Sum` for counters / `Average` for gauges, then summed across series — identical aggregation semantics to the previous implementation. Future buckets are filtered with `DateTimeOffset.UtcNow` (UTC-safe).
- `MetricsExtensions.Rate` — internal per-pair counter buckets switched from server-local (`ToLocalTime` + `ts.Offset`) to UTC (`UtcDateTime` + `TimeSpan.Zero`). No behavior change on UTC servers; removes the latent server-TZ dependency.

### Backend — UTC defaults on other dated handlers

- `GetApplicationTraces.Handler.cs`, `GetApplicationUsage.Handler.cs`, `GetMetricTotals.Handler.cs` — `DateTime.Today` defaults replaced with `DateTime.UtcNow.Date`. Per the audit, these endpoints are pure row filters / Counter-sum-invariant (`GetMetricTotals` is only consumed by `GetLlmUsage` over Counter metrics), so they don't need `tz` propagation.

### Frontend

- `apps/ui/src/extensions.tsx` — new helper `getBrowserTz()` returning `Intl.DateTimeFormat().resolvedOptions().timeZone`.
- `apps/ui/src/pages/applications/charts/{cpu-usage,memory-usage,bytes-in,bytes-out}.tsx` and `apps/ui/src/pages/llms/charts/tokens-usage.tsx` — each fetch appends `&tz=${encodeURIComponent(getBrowserTz())}`; each renders a `ChartColumn`-iconed "No data captured yet for this metric" placeholder when `data.values` is empty (instead of the destructive `<Alert>`).
- `apps/ui/src/components/date-time-range-picker.tsx` — presets ending "today" (`today`, `last7`, `last14`, `last30`, `thisWeek`, `thisMonth`) now end at `now` instead of `endOfDay(now)` so the chart isn't padded with future-zero bars. `yesterday`, `lastWeek`, `lastMonth` are unchanged.

## API behavior change

`GET /api/applications/{name}/metrics/{metric}` and `GET /api/llms/{llmId}/metrics/{metric}` previously returned `404 NotFoundException` when no `Metric` series matched the labels (i.e. the metric had never been recorded). They now return `200 { values: [] }` in that case.

The `[ProducesResponseType(Status404NotFound)]` decoration on both controller actions remains accurate — 404 is still returned when the parent **application** / **LLM** itself doesn't exist (the auth check in each handler throws `NotFoundException` before delegating to `GetMetrics`). Only the "metric series never recorded" sub-case shifted from 404 to 200.

Any external consumer that distinguished "metric never recorded" from "metric exists but empty in the requested window" loses that distinction. Internal callers (the UI) treat both as the empty-state.

## Out of scope (follow-up)

- No unit-test coverage for the new bucketing math. The repo has no API test project (`pnpm nx test api` is a no-op per `apps/api/CLAUDE.md`). Concrete first cases worth covering when an `xUnit` project is bootstrapped: Madrid spring-forward day, Sao Paulo (no DST as of 2019), bad `tz` id, empty `metricValues`, exact-boundary window (`from=00:00, to=01:00`).
- The 5 chart components each carry an inlined empty-state block; treating 5 as the soft ceiling for inline duplication, a 6th chart should trigger extraction into a shared `<ChartEmptyState title=… />`.
- No client-side TZ override (a Madrid user always sees Madrid metrics). Easy to add later if requested.

## Verification

- `pnpm nx build api` and `pnpm nx build ui` both succeed.
- The original failing live URL — `…/memory_usage?from=2026-04-30T22:00:00Z&to=2026-05-25T21:59:59Z&tz=Europe/Madrid` — once deployed will now produce a final bucket (`2026-05-24T22:00:00+00:00`) whose value averages the May 25 Madrid-local samples (~447 MB), not 0.
- Runtime checks (browser TZ override DevTools, DST historical window, `tz=Mars/Olympus` fallback) are listed step-by-step in the approved implementation plan.
