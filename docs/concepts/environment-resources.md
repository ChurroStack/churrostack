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
| **Requested** | blue | What the cluster reserves *right now* — Σ of each running app's **request** (CPU/memory) over deployments in `Running`/`Starting` state, per-deployment. The request is resolved from the size catalog (`Environment.Definition.Sizes[].Requests`); CPU/memory use the request, GPU/storage use `Application.Size` as-is | `ApplicationDeployment` rows + the size catalog |
| **Allocated** | gray | Total configured intent — Σ `Application.Size` (the **limit**/burst ceiling) over every app in the env (running or stopped) | `Application.Size` rows |
| **Quota** | track background | Base ceiling for the environment, parsed from `Environment.Definition.Limits`. Admission uses the **effective** ceiling `Quota × overcommit factor` (see [Overcommit](#overcommit)) | Environment definition |

### Why the distinction matters

- **Used vs Requested** — Used can sit well below Requested (idle workloads
  still reserve their request) and can also spike *above* Requested under burst
  (Kubernetes burstable QoS). The cluster guarantees Requested; Used is what
  actually shows up on the meter.
- **Requested vs Allocated** — Requested is the sum of **requests** (what the
  cluster reserves); Allocated is the sum of **limits** (the burst ceilings the
  apps could reach). Because the size catalog sets `requests < limits`,
  `Allocated − Requested` is the environment's **burst headroom**. Stopping an
  app drops it from Requested (and Allocated keeps it — stopping does not refund
  configured intent).
- **Quota** is what the platform enforces, against **Requested**, after scaling
  by the per-environment overcommit factor. You can over-allocate limits freely
  (Allocated may exceed Quota — that is the point of overcommit); what is capped
  is the sum of running **requests** against `Quota × factor`.

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
  + `.Handler.cs`) sums each running app's **request** (resolved from the size
  catalog via `SizeRecommendation.ResolveRequest`, falling back to the limit
  when no catalog entry matches) over every deployment with
  `ExecutionStatus ∈ {Running, Starting}` in the target environment and
  compares to the **effective** ceiling `Quota × overcommit factor` (see
  [Overcommit](#overcommit)). This is the same per-deployment request sum that
  `GetEnvironmentTotals` exposes as **Requested**, so the UI bar and the
  enforcement decision stay consistent — if the blue segment fits under the
  ceiling, the start succeeds. Memory additionally enforces a **burst ceiling**
  on the sum of running memory *limits* (`Quota_mem × MemoryBurst`, default
  `1.0` = `Σ limits ≤ quota`, i.e. no memory overcommit until an operator raises
  it) so simultaneous bursts stay bounded.

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
  `churros_tenant:{accountId}:env:{envId}:resource_lock` (120 s TTL, 5 s
  wait) before calling `EnsureEnvironmentRunningQuota`. The TTL is sized to
  comfortably exceed runner round-trip for typical K8s deploys; if you see
  it expiring under load, prefer adding lock renewal in `RedisLockService`
  over silently bumping it further.
  `UpdateApplicationHandler` and `DeployApplicationHandler` hold the lock
  through `SaveChangesAsync`. `DeployApplicationHandler` also flips the
  row's `ExecutionStatus` to `Starting` (for both new and re-deploy paths)
  when the deploy will add a running instance, so the saved state matches
  what the racing check counts; `ScrapeDeploymentStateJob` then reconciles
  to `Running` (or back to `Stopped` if the pod fails to schedule).

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

## Overcommit

The size catalog deliberately sets `requests < limits` (CPU request ≈ ½ limit,
memory ≈ ¼ limit), so the cluster schedules against the small request while the
app bursts up to its limit. Admission accounts by **request** (above), which is
the baseline reclaim. On top of that, each environment carries optional
overcommit factors on `Environment.Definition.Overcommit`:

| Factor | Applies to | Default | Notes |
|---|---|---|---|
| `Cpu` | `Σ cpuRequest(running) ≤ Quota_cpu × Cpu` | `1.0` | CPU over-limit is throttled (safe) → may be raised aggressively. |
| `Memory` | `Σ memRequest(running) ≤ Quota_mem × Memory` | `1.0` | Keep ≈ 1.0 so requests stay schedulable on dedicated capacity. |
| `MemoryBurst` | `Σ memLimit(running) ≤ Quota_mem × MemoryBurst` | `1.0` | Memory over-limit is **OOM-killed** → conservative. Default `1.0` = `Σ limits ≤ quota` (no memory overcommit, same as before). Set e.g. `1.5` to allow ~1.5× burst. |

All factors default to **1.0 (overcommit off)**. Even then CPU reclaims ~2× for free, because
admission now charges the request (≈ ½ the limit) rather than the limit. Memory capacity is
unchanged until an operator raises `MemoryBurst`.

- **Source.** The factors originate in the runner config
  (`Kubernetes:Overcommit:{Cpu,Memory,MemoryBurst}`) and flow into
  `Environment.Definition` on `ConnectEnvironment`, exactly like `Limits`. There
  is no per-environment UI knob — it is operator configuration. A config change
  takes effect the next time the environment is connected (the **Connect & Sync**
  action / `POST /api/environments/{name}/connect`), which overwrites
  `Environment.Definition` wholesale; it is **not** picked up automatically.
- **Dedicated capacity.** The Kubernetes scheduler places pods by request. On a
  dedicated node pool, set `Quota` at or below `Σ node allocatable − DaemonSet/
  system-reserved`. `Cpu > 1` means `Σ requests` can exceed allocatable and a
  pod may sit `Pending`; prefer right-sized requests over a large `Cpu` factor.
- **Totals.** `GetEnvironmentTotals` returns both `Quota` (base) and `Ceiling`
  (`Quota × factor`; memory uses `MemoryBurst`). The UI bar scales to `Ceiling`,
  marks the base `Quota`, and only flags Allocated amber when it exceeds the
  ceiling.

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
→ `EnvironmentTotalsItem` → `ResourceTotal { Used, Requested, Allocated, Quota, Ceiling }`
(CPU in cores, memory and storage in bytes, GPU as a count). `Ceiling` is the
effective admission ceiling `Quota × overcommit factor` and equals `Quota` when
no overcommit is configured.

The header bar component
(`apps/ui/src/pages/environments/environment-totals-bar.tsx`) draws them as a
single layered track:

```
┌─────────────────────────────────────────────┐  ← track (full width = Ceiling = Quota × factor)
│████░░░░░░░░░░░░░░░░░▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓│  ← gray = Allocated; ▓ = overcommit band (Quota→Ceiling)
│███░░░░░░░░░░░░░░░░░░│░░░░░░░░░░░░░░░░░░░░░░░░│  ← blue = Requested;  │ = base-Quota marker
│██░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│  ← green = Used        (drawn on top)
└─────────────────────────────────────────────┘
0     Used  Requested        Quota          Ceiling
```

When an overcommit factor is set the track scales to **Ceiling**, a striped band
marks the `Quota → Ceiling` headroom, and a thin line marks the base Quota;
Allocated turns amber only when it exceeds the Ceiling. With no overcommit,
Ceiling == Quota and the track is the plain layered bar. Hover to see the
numbers and their percentages relative to Quota. Environments without a
configured quota self-scale the track to `max(Allocated, Requested, Used)` and
omit the percentage suffix.

## Related

- Postmortem covering the move from runner-side to control-plane enforcement:
  [`../postmortems/2026-05-25-runtime-cpu-memory-quota/README.md`](../postmortems/2026-05-25-runtime-cpu-memory-quota/README.md)
