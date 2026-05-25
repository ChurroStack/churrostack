# Runtime CPU/Memory quota — enforce on Deploy too

**Date:** 2026-05-25
**Area:** `apps/api` quota enforcement
**Follow-up to:** [`../2026-05-25-runtime-cpu-memory-quota/README.md`](../2026-05-25-runtime-cpu-memory-quota/README.md)

## Trigger

After the earlier postmortem moved CPU/Memory enforcement to the API control
plane and gated it on `ExecutionStatus`, `DeployApplicationHandler` was left
out — the design assumption was "Deploy is free; Start is enforced." Reading
the code revealed that assumption was wrong: applying the deployment manifest
already causes the cluster to schedule a pod, so Deploy can silently breach
the environment quota.

Concrete bypass:

1. `Stop` an app (no check).
2. `Update` its `Size` to something larger than the env quota —
   `UpdateApplicationHandler` skips the check when no `Running`/`Starting`
   deployment exists.
3. Click `Deploy` — manifest applied with the new oversize `Size.requests`;
   pod scheduled. Quota was never enforced.

A first-`Deploy` on a brand-new app has the same effect.

## Root cause

`DeployApplicationHandler.Handle`
(`apps/api/src/ChurrOS.Api/Commands/Applications/DeployApplication.Handler.cs`)
calls `client.DeployAsync(...)` on the runner and persists the new
`ApplicationDeployment` row with `ExecutionStatus = Stopped`. The runner's
`CreateDeploymentHandler` unconditionally calls
`KubernetesService.ApplyYamlManifests(...)` — there is no ExecutionStatus
gating on the runner side, and the application templates render with
`replicas: 1` (e.g.
`apps/api/src/ChurrOS.Api/Resources/Templates/streamlit-application.yaml:57`).
Kubernetes schedules the pod immediately. `ScrapeDeploymentStateJob.cs:90-110`
later flips `ExecutionStatus` from `Stopped` → `Starting` → `Running` based on
the observed replica count.

So cluster usage happens before any ChurrOS `Start` call, but the
`EnsureEnvironmentRunningQuota` check was only wired into `Start` and `Update`.

## Fix

`DeployApplicationHandler` now mirrors `StartApplicationHandler`'s
quota-enforcement pattern when the deploy will add a running instance:

- Injects `ILockService` and `ITenantResolver`.
- Computes `addsRunningInstance = deployment is null
  || deployment.ExecutionStatus == Stopped
  || deployment.ExecutionStatus == Stopping`. Re-deploying a `Running` or
  `Starting` deployment is a no-op for the running totals and skips both the
  lock and the check (the manifest replace doesn't change replica count).
- When `addsRunningInstance` is true, acquires
  `churros_tenant:{accountId}:env:{envId}:resource_lock` (30 s TTL, 5 s wait —
  same key the Start and Update handlers use), sends
  `EnsureEnvironmentRunningQuota(envId, appId, app.Size,
  EnsureRunningQuotaMode.Start)`, then proceeds with `client.DeployAsync` and
  the row insert/update.
- Holds the lock through `SaveChangesAsync` (try/finally) so the new
  deployment row is visible to any racing Start/Deploy/Update check before the
  lock is released — same pattern as `UpdateApplicationHandler`.

No changes to `EnsureEnvironmentRunningQuota` itself — `Start` mode with
`NewSize = app.Size` is exactly the right contribution for a single new
running instance, whether the app is Application mode (1 deployment) or
Workspace mode (N deployments, one per owner — each Deploy adds one).

## Out of scope

- The Workspace per-owner deploy still adds one instance worth at a time;
  bulk-deploying all owners simultaneously isn't a flow that exists.
- Same caveat as the earlier postmortem: cluster-side scheduling remains the
  ultimate enforcement against workloads outside ChurroStack.

## Verification

- `pnpm nx build api` succeeds.
- The `docs/concepts/environment-resources.md` enforcement section was updated
  to list Deploy alongside Start and Update so future work doesn't repeat the
  same assumption gap.
- Functional E2E (manual) against a dev env with
  `Definition.Limits = { Cpu: "2", Memory: "4Gi" }`:
  - Reproduce the bypass: stop a 1-core app, update its size to `Cpu: "3"`,
    Deploy → now fails with `"The environment CPU quota has been exceeded."`
    (previously succeeded and overbooked the env).
  - Brand-new app with `Cpu: "3"` → first Deploy fails with the same message.
  - Re-Deploy a `Running` app (no size change) → succeeds, no lock acquired
    (check is skipped because re-deploying a running deployment doesn't add an
    instance).
