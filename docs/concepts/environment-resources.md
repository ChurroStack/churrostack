# Environment Resources — Used / Requested / Allocated / Quota

ChurroStack tracks four distinct resource numbers per environment for CPU,
memory, GPU, and storage. They look similar but answer different questions and
have different data sources. Mixing them up is the most common source of
"why does the bar say X but the cluster says Y" confusion, so this is the
canonical vocabulary used across the API, UI, and any future tooling.

## The four concepts

| Name | UI color | What it measures | Data source |
|---|---|---|---|
| **Used** | green | Live CPU/memory the cluster is currently burning | `ScrapeMetricsJob` → Redis hash `churros_tenant:{accountId}:app:{appId}:resource_usage` (1 h TTL) |
| **Requested** | blue | What the cluster has reserved *right now* — Σ `Application.Size` over deployments in `Running` or `Starting` state, per-deployment | `ApplicationDeployment` rows joined to `Application.Size` |
| **Allocated** | gray | Total configured intent — Σ `Application.Size` over every app in the env (running or stopped) | `Application.Size` rows |
| **Quota** | track background | Hard ceiling for the environment, parsed from `Environment.Definition.Limits` | Environment definition |

### Why the distinction matters

- **Used vs Requested** — Used can sit well below Requested (idle workloads
  still reserve their request) and can also spike *above* Requested under burst
  (Kubernetes burstable QoS). The cluster guarantees Requested; Used is what
  actually shows up on the meter.
- **Requested vs Allocated** — Stopping an app does not refund its size from
  Allocated; it does drop it from Requested. So `Allocated − Requested` is the
  amount of "configured but not currently running" budget an environment has.
- **Quota** is what the platform actually enforces. It applies to Requested,
  not Allocated — you can over-allocate freely as long as you don't try to
  run everything at once.

### Mapping to Kubernetes

The naming is deliberately K8s-aligned:

- **Used** ≈ what `metrics.k8s.io` reports (`kubectl top pod`).
- **Requested** ≈ the sum of `resources.requests` across the pods the cluster
  is currently scheduling for this environment. A Workspace app with N active
  per-user deployments contributes `N × Size`, matching what the scheduler
  actually sees.
- **Allocated** has no direct K8s counterpart — it's a ChurroStack-side
  concept that captures *intent across all apps regardless of replica count*.
- **Quota** ≈ Kubernetes `ResourceQuota`, but enforced by ChurroStack in the
  control plane (see Enforcement below) rather than by the cluster.

## Enforcement

CPU/Memory quotas are enforced **at the moment an app would start consuming
resources**. The check lives in the API control plane — the Kubernetes runner
is no longer involved in quota decisions.

"Consuming resources" includes both `Start` and `Deploy`: the application
templates render with `replicas: 1`, so applying the manifest schedules a pod
immediately even though the `ApplicationDeployment` row is initially marked
`Stopped` until `ScrapeDeploymentStateJob` reconciles it. Treat Deploy as a
Start for quota purposes.

### Where the check runs

- `EnsureEnvironmentRunningQuota`
  (`apps/api/src/ChurrOS.Api/Commands/Environment/EnsureEnvironmentRunningQuota.cs`
  + `.Handler.cs`) sums `Application.Size` over every deployment with
  `ExecutionStatus ∈ {Running, Starting}` in the target environment and
  compares to `Environment.Definition.Limits`. This is the same per-deployment
  sum that `GetEnvironmentTotals` exposes as **Requested**, so the UI bar and
  the enforcement decision are always consistent — if the blue segment fits,
  the start succeeds.

- Invoked from three places:
  - `StartApplicationHandler` — `Mode = Start`, candidate's contribution is
    `NewSize`.
  - `DeployApplicationHandler` — `Mode = Start`, candidate's contribution is
    `NewSize`. Only runs when the deploy will add a running instance, i.e.
    the target deployment is new or its current `ExecutionStatus` is
    `Stopped`/`Stopping`. Re-deploying a `Running`/`Starting` deployment is a
    no-op for the running totals and skips the check.
  - `UpdateApplicationHandler` — only when `size` is in the request body *and*
    the app already has a `Running`/`Starting` deployment. `Mode = Update`,
    contribution is `runningDeploymentCount × (NewSize − OldSize)`. Updating
    the size of a stopped app skips the check entirely.

- Failure throws
  `InvalidOperationException("The environment CPU/Memory quota has been exceeded.")`
  which bubbles up as a 4xx response.

### Concurrency: per-environment Redis lock

Two starts on the same environment could each pass the check before either's
`ExecutionStatus` flips to `Running`. To close that window:

- `ILockService` / `RedisLockService` (`apps/api/src/ChurrOS.Api/Services/`,
  registered as a singleton in `Program.cs`) wraps `StackExchange.Redis`
  `LockTakeAsync` / `LockReleaseAsync` with a GUID token so release only frees
  our own hold.
- `StartApplicationHandler`, `DeployApplicationHandler`, and
  `UpdateApplicationHandler` acquire
  `churros_tenant:{accountId}:env:{envId}:resource_lock` (30 s TTL, 5 s wait)
  before calling `EnsureEnvironmentRunningQuota`.
  `UpdateApplicationHandler` and `DeployApplicationHandler` hold the lock
  through `SaveChangesAsync` so the size change / new deployment row is
  visible to any racing check.

### What is *not* enforced

- **Allocated > Quota** is allowed. You can configure apps whose sizes sum
  beyond the env limit as long as you don't try to run them simultaneously.
  The UI surfaces this state by flipping the gray segment to amber.
- **Account-level "max number of applications"** is a separate check in
  `CreateApplication.Handler.cs` (count-based, not resource-based) and is
  unaffected by any of the above.
- **GPU and storage quotas** are tracked in the totals DTO but
  `EnsureEnvironmentRunningQuota` only validates CPU and memory. The other
  two resources are surfaced for visibility, not enforcement.
- **Cluster-level scheduling**. The Redis lock + `Running OR Starting`
  predicate close the obvious overflow window on the ChurroStack side, but a
  pod can still fail to schedule if the underlying cluster is oversubscribed
  by workloads outside ChurroStack.

## Where each value is computed

| Value | Code |
|---|---|
| Used (per app) | `apps/api/src/ChurrOS.Api/Jobs/ScrapeMetricsJob.cs` — writes Redis hashes from gRPC runner metrics |
| Used (env total) | `apps/api/src/ChurrOS.Api/Commands/Environment/GetEnvironmentTotals.Handler.cs` — sums the per-app Redis hashes |
| Requested (env) | Same handler — separate query over `ApplicationDeployment` filtered by `ExecutionStatus` |
| Requested (enforcement) | `apps/api/src/ChurrOS.Api/Commands/Environment/EnsureEnvironmentRunningQuota.Handler.cs` — same query shape |
| Allocated (env) | Same `GetEnvironmentTotals` handler — sum over all `Application.Size` in the env |
| Quota | `Environment.Definition.Limits`, parsed via `TryParseCpuToCores` / `TryParseMemoryToBytes` |

## UI surfaces

The single source of truth for the env-level numbers is
`GET /api/environments/{name}/totals`
→ `EnvironmentTotalsItem` → `ResourceTotal { Used, Requested, Allocated, Quota }`
(CPU in cores, memory and storage in bytes, GPU as a count).

The header bar component
(`apps/ui/src/pages/environments/environment-totals-bar.tsx`) draws them as a
single layered track:

```
┌─────────────────────────────────────────────┐  ← track (full width = Quota)
│██████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← gray   = Allocated   (drawn first)
│████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← blue   = Requested   (drawn over gray)
│██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← green  = Used        (drawn on top)
└─────────────────────────────────────────────┘
0       Used   Requested    Allocated      Quota
```

Hover the bar to see the four numbers and their percentages relative to Quota.
Environments without a configured quota self-scale the track to
`max(Allocated, Requested, Used)` and omit the percentage suffix.

## Related

- Postmortem covering the move from runner-side to control-plane enforcement:
  [`../postmortems/2026-05-25-runtime-cpu-memory-quota/README.md`](../postmortems/2026-05-25-runtime-cpu-memory-quota/README.md)
