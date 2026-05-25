# Runtime CPU/Memory quota — enforce on running apps only

**Date:** 2026-05-25
**Area:** `apps/api` quota enforcement, `apps/churrun-kubernetes` deployment flow

## Trigger

Environment CPU/Memory was enforced at deployment-creation time inside the
Kubernetes runner. Users hit "quota exceeded" when creating or deploying a new
application even though most of the other apps in the environment were stopped
and consuming nothing. The intended contract is: applications can be created
and deployed freely; the environment budget only governs which apps can be
*running simultaneously*. The per-account "max number of applications" limit
stays as a hard create-time check.

## Root cause

`apps/churrun-kubernetes/src/Commands/Environment/EnsureEnvironmentHasQuota.Handler.cs`
summed every `Deployment.Size` in the runner's local SQLite — there is no
notion of "running" on that side; the runner persists deployments regardless
of replica count. So a fresh deploy of a 1-core app into an environment with
2-core capacity failed if there already were two other 1-core apps recorded,
even when those were scaled to zero replicas. The check also ran from
`CreateDeployment` (deployment apply), not from the lifecycle boundary that
actually consumes CPU/Memory (`StartApplication`).

The API control plane had no equivalent runtime check at all.

## Fix

Moved CPU/Memory enforcement to the API and gated it on `ExecutionStatus`.

- **New mediator command** `EnsureEnvironmentRunningQuota`
  (`apps/api/src/ChurrOS.Api/Commands/Environment/EnsureEnvironmentRunningQuota.cs`
  + `.Handler.cs`). Sums `Application.Size` over deployments where
  `ExecutionStatus` ∈ {`Running`, `Starting`} in the target environment
  (per-deployment, so Workspace apps with N active per-user instances count N
  times, matching what the cluster actually requests). For `Start` mode the
  candidate's contribution is `NewSize`; for `Update` mode it's
  `runningDeploymentCount × (NewSize − OldSize)`. Throws
  `InvalidOperationException("The environment CPU/Memory quota has been exceeded.")`
  with parity to the old runner messages.

- **`StartApplicationHandler`** (`apps/api/src/ChurrOS.Api/Commands/Applications/StartApplication.Handler.cs`)
  — acquires a per-environment Redis lock
  (`churros_tenant:{accountId}:env:{envId}:resource_lock`, 30 s TTL, 5 s wait)
  before sending `EnsureEnvironmentRunningQuota` and calling `client.StartAsync`.
  The lock closes the race window where two concurrent starts on the same env
  could both pass the check before either's `ExecutionStatus` updates.

- **`UpdateApplicationHandler`** (`apps/api/src/ChurrOS.Api/Commands/Applications/UpdateApplication.Handler.cs`)
  — when the request body includes `size`, the handler probes whether the app
  has any `Running`/`Starting` deployment. If yes, it acquires the same env
  lock, runs `EnsureEnvironmentRunningQuota` with the new size, and holds the
  lock through `SaveChangesAsync`. Stopped apps allow any size change with no
  check.

- **`ILockService` + `RedisLockService`**
  (`apps/api/src/ChurrOS.Api/Services/ILockService.cs`,
  `apps/api/src/ChurrOS.Api/Services/Redis/RedisLockService.cs`,
  registered as a singleton in `Program.cs`). Thin wrapper over
  StackExchange.Redis `LockTakeAsync` / `LockReleaseAsync` with a polling wait
  and a GUID token so release only frees our own hold.

- **Removed** the runner-side enforcement: deleted
  `apps/churrun-kubernetes/src/Commands/Environment/EnsureEnvironmentHasQuota.cs`
  + `.Handler.cs` and the call from `CreateDeployment.Handler.cs`. The runner
  no longer participates in CPU/Memory quota decisions — the control plane is
  authoritative.

The account-level applications count check in
`CreateApplication.Handler.cs:49-56` was **not** touched.

## Out of scope (follow-up)

- The lock is single-region by virtue of the existing single Redis. If the
  control plane is ever horizontally scaled across regions sharing only the
  Postgres data, this would need a different coordination layer.
- `ExecutionStatus` is reconciled asynchronously by `ScrapeDeploymentStateJob`.
  The Redis lock + the "Running OR Starting" predicate together close the
  obvious overflow window, but cluster-side scheduling remains the ultimate
  enforcement: a pod can still fail to schedule if the cluster itself is
  oversubscribed by other workloads outside ChurroStack.

## Verification

- `pnpm nx build api` and `pnpm nx build churrun-kubernetes` both succeed.
- Functional E2E (manual) against a dev environment with
  `Definition.Limits = { Cpu: "2", Memory: "4Gi" }`:
  - Create three apps each requesting `Size = { Cpu: "1", Memory: "2Gi" }` →
    all creations and deployments succeed (previously the third would have
    failed at deploy time).
  - `Start` app 1 → success; app 2 → success; app 3 → fails with "The
    environment CPU quota has been exceeded."
  - `Stop` app 2; `Start` app 3 → success.
  - `Update` app 1's size to `Cpu: "3"` while running → fails with the same
    message; stop app 1, repeat → success.
- `GetEnvironmentTotals` continues to return the same `Requested`/`Used`/`Total`
  shape; no DTO/schema change.
- Concurrency smoke: two `StartApplication` calls on apps whose combined size
  would breach the budget — only one succeeds (Redis lock).
