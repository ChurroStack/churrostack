# Fix member management: silent save failures + Manager authorization

**Date:** 2026-09-22
**Area:** `apps/api` environment/application/LLM ACL management, `apps/ui` Manage Access panels

## Trigger

Two reports on the Environments **Manage Access** tab:

1. Members were "not applied sometimes" — the server responded with success and the UI
   showed a success toast, but the member list did not reflect the change.
2. An environment Manager (a member holding `Permission.Manage` on the environment's ACL)
   could not manage other members — only account Administrators could.

## Root cause

This was a cluster of independent defects across the backend and the UI, several of which
produced the exact same symptom ("success, nothing changed"), which is why it read as
intermittent rather than a single reproducible bug.

**Backend, `apps/api`:**

- `UpdateEnvironment.Handler.cs` gated the entire PATCH on `EnsureHasRole(Administrator)`
  instead of the ACL-aware `IsAdminOrHasAcl(env.AclId, Permission.Manage)` used by every other
  environment command — the environment PATCH was the only one still admin-only.
- The same handler treated `Members: []` (or any array with `Length == 0`) as "no change" and
  took a tag-only fast path, so clearing the last member(s) silently no-op'd with a 200.
- The shared ACL writer, `UpdateAclAsync`, called `ExecuteDeleteAsync` to remove stale members
  immediately — outside, and before, the caller's own `SaveChangesAsync`. A failure later in the
  same request (including the next defect) left members deleted without the corresponding
  additions ever landing.
- `UpdateAclAsync`'s delete filter and existing-member lookup matched identity names
  case-sensitively, while `GetIdentityId` resolves names case-insensitively. A payload whose
  casing differed from the stored (always lower-cased) name took the delete-then-re-add path
  instead of an update, and could collide on the composite primary key.
- `GetIdentityId` returned `0` for an unresolvable name with no caller-side check, so
  `UpdateAclAsync` would insert an `AclMember` row pointing at identity `0`. The failed lookup
  was also cached under a **raw-cased** key for 5 minutes, while the identity-upsert path only
  ever purged the **lower-cased** key — so a user created and immediately added as a member
  could resolve to `0` for up to 5 minutes.

**Frontend, `apps/ui`:** the same near-identical panel exists for Environments, Applications and
LLMs (`pages/{environments,applications,llms}/panels/members-panel.tsx`), and all three shared:

- The form-reseeding `useEffect` ran unconditionally on every prop refresh (including ones
  triggered by unrelated background SignalR notifications), silently discarding any in-progress,
  unsaved edit — the user would then save the *stale* list they were still looking at.
- The Save button bypassed `form.handleSubmit`, so the zod validation on the member rows never
  ran, and a freshly-added-but-not-yet-filled-in row could be submitted as-is.
- The PATCH response — the server's authoritative post-save state — was discarded; nothing
  re-synced the form or the parent page from it.
- The Manage Access tab was not gated by permission at all (environments only had the data to do
  so; applications and LLMs additionally lacked the `useMyPermission` call), so a read-only user
  saw a fully editable form that could only end in a 403 on save.

## Fix

- `UpdateEnvironment.Handler.cs` now gates on `IsAdminOrHasAcl(environment.AclId,
  Permission.Manage)`, matching every other environment command; an environment Manager gets
  full control (add/remove/change any role, including Manager) with no self-escalation guard,
  consistent with how ACLs are checked everywhere else in this codebase.
- The tag-only fast path and the runner reconnect/template-registration/`Provisioning` flip that
  used to run on every member edit were removed from the PATCH handler; that work already exists
  behind the **Connect & Sync** button. Member changes are now gated on `Members is not null`, so
  an explicit empty array correctly clears the ACL.
- `UpdateAclAsync` now stages removals with `RemoveRange` instead of executing them immediately,
  so removals, updates and additions all land atomically in the caller's single
  `SaveChangesAsync` — a failure partway through (an unresolvable identity) leaves the ACL
  untouched. Name matching (both the removal diff and the existing-member lookup) is now
  case-insensitive, matching identity resolution, and the requested set is deduped by name so a
  duplicate can't violate the composite primary key. A `0` identity id now throws a
  `NotFoundException` naming the identity instead of writing a corrupt row. This helper is shared
  by environments, applications and LLMs, so all three benefit.
- `GetIdentityId`'s cache key is now lower-cased so the existing lower-cased invalidation prefix
  (already added by identity upsert) actually matches it.
- All three Manage Access panels were rebuilt on a new shared `useMembersForm` hook
  (`apps/ui/src/components/members-editor.tsx`) that: reseeds from props only when the form has
  no unsaved edit (`form.formState.isDirty`, read in the hook body so RHF's lazily-subscribed
  proxy stays live — reading it only inside the effect would freeze it at its initial value),
  always validates through `form.handleSubmit` (with an `onInvalid` handler that toasts, so a
  rejected submit — e.g. a blank identity row, or a zero-member array — is visible instead of a
  silent no-op), and reseeds from the server's response on a successful save before handing it to
  the caller. The environment panel additionally tracks a `tagsDirty` flag for its separate tags
  input using the same guard. `MembersEditor` grew an optional `disabled` prop; each panel now
  computes `useMyPermission(...).canManage` and renders read-only when false (Save disabled, the
  editor fields read-only). The parent pages (`environment.tsx`, `application.tsx`, `llm.tsx`)
  now guard every `setX(result.data)` call against an aborted/superseded fetch and pass the PATCH
  response back into the panel via an `onUpdated` callback, so there is one source of truth (the
  server) instead of two.

## Verification

1. `pnpm nx test api`: 8 passed (4 pre-existing + 4 new), 0 failed.
2. `pnpm nx build api`: succeeded, 0 errors.
3. `pnpm nx build ui` (`tsc -b && vite build`): succeeded, 0 errors.
4. `pnpm nx lint ui`: 0 errors (199 warnings, all pre-existing categories — no new error class
   introduced; the two new warnings on the touched files match a pattern — syncing local state
   from a prop inside an effect, and co-locating a hook with its component — already present
   elsewhere in this codebase).
5. New regression tests in
   `apps/api/src/ChurrOS.Api.Tests/Utils/UpdateAclAsyncTests.cs`, mirroring the existing
   `AuthorizationCacheExtensionsTests.cs` / `UpsertIdentityHandlerTests.cs` pattern (EF Core
   InMemory provider, NSubstitute for `IMediator`):
   - an empty member array removes every existing row;
   - a name differing only in case updates the existing row instead of taking the
     delete-and-re-add path (proven by making the identity-resolution mock throw if it is ever
     called for that member);
   - an unresolvable identity throws `NotFoundException` and — verified by reading the row back
     through a separate `ChurrosDbContext` instance before the caller's own `SaveChangesAsync`
     would have run — leaves the pre-existing member completely untouched;
   - a permission change on an existing member persists.
6. **Not yet performed:** manual verification in a running instance (sign in as a Manager and as
   a Collaborator on an environment, application and LLM; trigger a background notification
   while an edit is pending; confirm `PATCH` with `"members": []` empties the ACL). This requires
   a running API + UI + database and was out of reach in this session — do this before shipping.
