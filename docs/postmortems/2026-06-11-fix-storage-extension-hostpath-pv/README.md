# Fix storage extension `hostPath` — invalid PersistentVolumeClaim, plus multi-mount + "Map to"

**Date:** 2026-06-11
**Area:** storage extension template
(`apps/api/src/ChurrOS.Api/Resources/Templates/Extensions/storage-extension.yaml`),
Kubernetes runner (`apps/churrun-kubernetes`), application Settings UI
(`apps/ui/src/pages/applications/panels`)

## Trigger

A storage extension configured to use the `hostPath` storage class never
produced a usable volume. The Settings page also only allowed a **single**
storage mount per application, and there was no way to bind a mount to an
environment-controlled local path.

## Root cause

The `{{~ if storageClass && storageClass == "hostPath" ~}}` branch of the
storage extension template emitted a **`PersistentVolumeClaim`** with a
`hostPath:` field:

```yaml
kind: PersistentVolumeClaim
spec:
  ...
  hostPath:
    path: "/var/lib/churrun-{{ target }}-{{ id }}"
    type: DirectoryOrCreate
```

`hostPath` is **not a valid field on a PersistentVolumeClaim** — it belongs on a
`PersistentVolume`. The PVC was therefore either rejected or bound to nothing,
and the host path was a hard-coded, non-configurable location. The runner's
`KubernetesService.ApplyYamlManifests` also had **no `PersistentVolume` case**,
so it could not have created a PV even if the template emitted one
(`NotSupportedException`).

## Fix

1. **Template** — split the `hostPath` branch into a proper **`PersistentVolume`**
   (carrying `hostPath.path`, `Retain` reclaim policy, a `claimRef` pre-binding)
   **+ a `PersistentVolumeClaim`** statically bound to it via
   `storageClassName: ""` and `volumeName`. The host path is now driven by a new
   `parameters.hostPath` parameter ("Map to"), falling back to the implicit
   `/var/lib/churrun-{target}-{id}` path when in hostPath mode without a mapping.
2. **Runner** — added a `PersistentVolume` case (cluster-scoped create + 409
   fallback) to `ApplyYamlManifests`, a `hostPaths` catalog to `sizes.yaml`
   surfaced on `EnvironmentDefinition`, and a deploy-time check that any requested
   `hostPath` is one of the environment's managed paths.
3. **API** — a user-scoped `GET /api/environments/{name}/host-paths` endpoint
   (filtered by identity/group against the YAML allow-list), save-time validation
   in `UpdateApplication` rejecting unauthorized host paths, and multi-instance
   extension support (resolving the template for additional storage rows like
   `storage-2` via a client-supplied `templateName`).
4. **UI** — the single Storage card became an editable **table** (Mount path /
   Size / Map to), emitting one storage extension per row.

## Verification

- `?dry=true` deploy renders one PV (`hostPath.path`) + one bound PVC, with **no**
  `hostPath` on the PVC; real-storage-class and `emptyDir` paths unchanged.
- `GET /api/environments/{name}/host-paths` returns only paths the caller may use;
  empty for non-members.
- Save with an unauthorized `hostPath` → 403 (API); direct runner deploy with an
  unmanaged path → 403 (runner).
- UI: multiple `com.churrostack.extension.storage` entries persist with distinct
  names and round-trip; "Map to" dropdown disabled when no paths are allowed.

## Notes / follow-ups

- The environment `Definition` is a stored snapshot refreshed on connect/update —
  editing `sizes.yaml` `hostPaths` requires re-connecting the environment to take
  effect (same as `sizes`).
- Deleting a storage row leaves its PV/PVC orphaned cluster-side (PV uses
  `Retain` to preserve data); reclaiming orphaned volumes is a separate follow-up.
