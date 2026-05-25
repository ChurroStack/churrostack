# Auto-Start / Auto-Stop

Two opt-in behaviours per application that let the platform spin up an app on demand and reclaim resources when it has been idle.

## Vocabulary

| Term | Meaning |
|---|---|
| **Auto-Start** | When a request arrives at `/share/{app}/{port}/...` for a *stopped* app with Auto-Start enabled, the proxy holds the request, fires `StartApplication`, then forwards once the deployment becomes ready (or returns `504` after the hold timeout). |
| **Auto-Stop** | A 5-minute Quartz cron (`AutoStopEvaluatorJob`) scans every tenant's apps and stops any with Auto-Stop enabled that have been *idle* for at least their configured threshold. |
| **Idle** | No HTTP request through the share proxy AND CPU below the activity threshold (`AutoStartConstants.CpuActivityCores`, 0.05 cores) for the full idle window. Either signal refreshes a single Redis key (`app:{id}:last_activity`), so "idle = key older than threshold". |
| **Cooldown** | A 60-second window after Auto-Stop fires. Requests during the cooldown receive `503` instead of triggering a new Auto-Start. Manual `Start` ignores the cooldown. |
| **Single-flight** | When many requests arrive concurrently for a stopped app, exactly one wins the Redis `SETNX` on `app:{id}:autostart_inflight` and triggers `StartApplication`; the rest poll the running flag. |

## Where the settings live

Per-app, stored inside `Application.Metadata` (the same JSON column that holds `description` — no schema migration needed):

```jsonc
{
  "description": "...",
  "autoStart": { "enabled": true },
  "autoStop":  { "enabled": true, "idleMinutes": 60 }
}
```

`idleMinutes` ∈ { 30, 60, 120, 240, 480 } via the settings UI dropdown. `AutoStartSettings.From(JsonElement?)` is the typed reader used by the YARP transform and the cron.

## Hot-path cache contract

Every request through `/share/*` must avoid Postgres on the steady state. Keys:

| Redis key | Writer | Reader | TTL | Invalidation |
|---|---|---|---|---|
| `app:{name}:share_route` | YARP transform (on miss, hydrate from DB) | YARP transform | 60 s | `Start`, `Stop`, `Update` handlers; `ScrapeDeploymentStateJob` on `ExecutionStatus` change; `ProxyConfigurationProvider.Add/RemoveApplication` |
| `app:{id}:running` | `ScrapeDeploymentStateJob` when status flips to `Running` | `AutoStartCoordinator` (hold-path pollers) | 24 h | `DEL` on transition away from Running; `DEL` by `StopApplication` |
| `app:{id}:last_activity` | YARP transform (throttled to ≤1 write / 30 s / replica) + `ScrapeMetricsJob` when CPU ≥ 0.05 cores | `AutoStopEvaluatorJob` cron | 48 h | Overwrite-on-activity |
| `app:{id}:autostart_inflight` | `AutoStartCoordinator` (`SET NX EX 180`) | Coordinator (single-flight gate) | 180 s | Auto-expires only |
| `app:{id}:autostart_cooldown` | `StopApplication` when `SetCooldown = true` AND `BypassAcl = true` | Coordinator | 60 s | Auto-expires; cleared by a manual `Start` (`!BypassAcl`) so an explicit user override is honoured immediately |
| `app:{id}:autostop_inflight` | `AutoStopEvaluatorJob` before dispatching Stop (`SET NX EX 300`) | `AutoStopEvaluatorJob` (skip if claimed) | 5 min | Auto-expires; prevents duplicate Stop on the next 5-min cron tick while `ScrapeDeploymentStateJob` is still observing the runner-side transition |

The constants live in `apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartConstants.cs`.

## Quota and ACL

- Auto-Start delegates to `StartApplication` with `BypassAcl = true`. The handler still calls `EnsureEnvironmentRunningQuota` (under the same per-env Redis lock as a manual start), so Auto-Start cannot exceed the environment's CPU/memory quota — see [`environment-resources.md`](environment-resources.md).
- `BypassAcl` skips the ACL check (the request came from the proxy, not an authenticated user). Manual starts continue to enforce ACLs.

## Failure modes

- **Redis unavailable** — the YARP transform falls back to per-request DB reads (no caching); the coordinator returns `503` rather than blocking forever.
- **Missed cache invalidation** — the route cache TTL bounds staleness at 60 s. The worst observable effect is one TTL window of forwarded-to-stopped requests (existing 5xx behaviour) or one TTL window of held requests that the cache thinks are still stopped.
- **Concurrent fan-in on a stopped app** — only one request triggers `StartApplication`; the rest poll the `running` flag every 500 ms and unblock together when the deployment is ready.

## Application mode only (V1)

`ApplicationMode.Workspace` is **excluded** from Auto-Start/Auto-Stop in V1. Workspace apps have per-user deployments while the Redis keys above (`running`, `autostart_inflight`, etc.) are app-scoped, which would conflate per-user state. The YARP transform short-circuits and passes through; `AutoStopEvaluatorJob` filters `Mode == Application` in SQL.

## Scheduled-job parity (`ApplicationHttpRequestJob`)

A user-configured schedule (`UpsertApplicationSchedule`) fires HTTP requests through `RunnerClient.HttpClient`, which talks **directly to the runner** — it does not go through the API's YARP pipeline, so the transform above never runs for it. To keep scheduled requests working when Auto-Stop has reclaimed an app, the job now:

1. Resolves the route info via `AutoStartCache.GetRouteAsync` (cached, shared with the transform).
2. For `ApplicationMode.Application` apps that are not Running, calls `AutoStartCoordinator.HoldUntilRunningAsync` with `bypassCooldown: true` — the schedule is system-initiated and the cooldown is a client-flap guard, not a global pause.
3. After a successful HTTP send, writes `app:{id}:last_activity` so a schedule that's the app's only traffic source still counts as keep-alive.

The `bypassCooldown` flag is the only knob the cron path needs that the user-driven path doesn't.

## Where to look

- Transform: `apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartTransform.cs`
- Coordinator: `apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartCoordinator.cs`
- Cache + Redis helpers: `apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartCache.cs`
- Cron job (auto-stop): `apps/api/src/ChurrOS.Api/Jobs/AutoStopEvaluatorJob.cs`
- Cron job (scheduled HTTP): `apps/api/src/ChurrOS.Api/Jobs/ApplicationHttpRequestJob.cs`
- Settings reader: `apps/api/src/ChurrOS.Api/Services/AutoStart/AutoStartSettings.cs`
- UI: `apps/ui/src/pages/applications/panels/settings-panel.tsx`
