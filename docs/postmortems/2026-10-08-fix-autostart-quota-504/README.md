# Auto-start admission failure surfaced as a slow 504

**Date:** 2026-10-08
**Area:** `apps/api` auto-start (`/share/*` activator) + environment quota admission

## Trigger

With the environment overcommit work (accounting running apps by their Kubernetes
*request* and adding a per-environment overcommit factor — see
[`docs/concepts/environment-resources.md`](../../concepts/environment-resources.md)),
the quota admission path became *hot*: an environment can legitimately refuse a
new start when it is at its effective ceiling. The existing auto-start path
handled that refusal badly.

## Root cause

`AutoStartCoordinator.FireStartAsync` ran the start on a background task and
**caught-and-only-logged** every exception, including the
`InvalidOperationException("The environment CPU/Memory quota has been exceeded.")`
thrown by `EnsureEnvironmentRunningQuota` and the lock-busy
`InvalidOperationException("Environment is busy, please retry.")`. It never
cleared the single-flight `app:{id}:autostart_inflight` key.

Consequences for a client request held in `HoldUntilRunningAsync`:

- the `app:{id}:running` flag never appeared, so the holder polled until the
  **300 s** `HoldTimeout` and received a **504** — for what is really a fast,
  deterministic "at capacity" condition;
- new requests within the 420 s inflight TTL became *followers* and also timed
  out, so one failed start poisoned a 7-minute window with slow 504s.

## Fix

Make admission failure fail **fast** and surface the reason, with a bounded
backoff so the env lock is not hammered.

- **`FireStartAsync`** (`apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartCoordinator.cs`):
  on a caught failure it now writes `app:{id}:start_failed` (reason, 10 s TTL via
  `AutoStartConstants.StartFailedTtl`) **before** deleting the inflight key. The
  reason is the `InvalidOperationException` message (safe, user-facing) or a
  generic fallback for other exceptions.
- **`HoldUntilRunningAsync`**: a new `HoldOutcome.Rejected`. Before claiming
  leadership *and* on every poll iteration it checks `start_failed`; if present it
  returns `Rejected` immediately. Because the backoff key blocks a new claim, the
  10 s window short-circuits every waiting/new request to a fast response instead
  of each becoming a fresh leader that re-runs the quota check under the env lock
  (thundering-herd guard). After the TTL, the next request retries the start.
- **`AutoStartTransform`**: `Rejected` → **503** with the recorded reason
  (e.g. "The environment CPU quota has been exceeded."), falling back to
  "Environment is at capacity; please retry." if the key already expired.
- **`AutoStartCache`**: `SetStartFailedAsync` / `GetStartFailedAsync` /
  `ClearInflightAsync` helpers; **`AutoStartConstants`**: `StartFailedTtl` +
  `StartFailedKey`.

`ApplicationHttpRequestJob` only checks `outcome != HoldOutcome.Running`, so the
new enum value needs no change there (a `Rejected` scheduled wake is skipped).

### Cache-key contract (changed/added)

| Key | Writer | Reader | TTL | Invalidation |
|---|---|---|---|---|
| `app:{id}:autostart_inflight` | `TryClaimStartAsync` (SET NX) | followers in `HoldUntilRunningAsync` | 420 s | **now also deleted by `FireStartAsync` on failure** |
| `app:{id}:start_failed` *(new)* | `FireStartAsync` on failure | `HoldUntilRunningAsync` (pre-claim + each poll) and `AutoStartTransform` (reason) | 10 s | TTL only — acts as the retry backoff |

## Verification

- Build: `pnpm nx build api` (0 errors).
- Behavioral: with an environment at its effective ceiling, an auto-start request
  to `/share/{app}` now returns **503 within ~1 s** carrying the quota reason, and
  `app:{id}:autostart_inflight` is cleared; a retry within 10 s also returns a fast
  503 without re-running the quota check; after 10 s the start is retried.
- Non-regression: a normal cold start (capacity available) still holds and
  forwards on `Running`; cooldown and timeout paths are unchanged.

## Out of scope / follow-up

- A pod that **passes** admission but cannot schedule (`Pending`, e.g. no single
  node fits its request) still hangs to the 300 s `HoldTimeout`. Surfacing the
  runner's `FailedScheduling` events through the activator would close that gap.
- Preemption (evicting an idle auto-start app to admit an active one) is a
  separate, later phase.
