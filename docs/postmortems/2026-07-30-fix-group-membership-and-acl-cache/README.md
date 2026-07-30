# Fix group membership persistence and authorization cache invalidation

**Date:** 2026-07-30  
**Area:** `apps/api` identity membership, ACL authorization caches, tenant resolution

## Trigger

Administrators reported that users added to an existing group disappeared after
save, and users granted access through a group could not immediately see the
assigned LLM. A caller could also supply an unverified `X-TENANT-ID` header.

## Root cause

`UpsertIdentityHandler` marked every existing membership row as deleted and
then added new instances for the complete requested set. Keeping an existing
member therefore tracked two entities with the same composite primary key,
preventing later group edits from being saved.

The same handler constructed its cache purge set from the membership row's
identity column even when the relevant counterpart was the group. ACL updates
only purged identities named directly in the ACL, so a group grant left its
users' inherited ACL cache entries stale until their one-minute expiry.

`MultiTenantMiddleware` executed a membership count for `X-TENANT-ID` but
returned the supplied tenant id without checking it. The first fix placed that
check behind a process-cache entry keyed only by identity name, so later tenant
headers and membership revocations could still reuse a stale result.

The first membership diff used `ExecuteDeleteAsync` before the final
`SaveChangesAsync`, allowing a failed request to commit removals without its
additions. Group cache expansion also ran for resource edits that did not
change ACL members, multiplying Redis prefix scans unnecessarily.

## Fix

- Membership edits now diff existing and requested ids: only removed rows are
  tracked for deletion and only new rows are inserted. Both apply atomically in
  the final `SaveChangesAsync`.
- Authorization-cache invalidation expands group ids to their current member
  user ids. The shared helper is used by identity, LLM, application, and
  environment ACL changes, and resource handlers call it only when ACL members
  were actually edited.
- The tenant header is accepted only when the authenticated identity belongs to
  that tenant; otherwise resolution falls back to the name lookup. Tenant
  membership is resolved on every request so header switches and revocations
  cannot reuse process-cached authorization.
- Added action-bound trace logs for membership diffs and authorization-cache
  purges (account and identity counts only).
- Added the `ChurrOS.Api.Tests` project with regression tests for tenant
  switching/revocation, deferred membership persistence, and group cache
  expansion.

## Verification

1. `dotnet test src/ChurrOS.slnx`: 4 passed, 0 failed.
2. `dotnet build src/ChurrOS.slnx --no-restore`: succeeded.
3. `pnpm build` in `apps/ui`: succeeded.
4. In the UI, create a group, add a member, reopen it, then add and remove
   further members; verify each save survives a reload. Repeat from a user's
   **Member of** editor.
5. Import the same identity workbook twice; the second import succeeds.
6. With Redis keys present for a group member, grant the group an LLM,
   application, or environment ACL and verify that member's identity cache
   prefix is removed immediately.
7. With a regular user's token, request an API endpoint using another tenant's
   `X-TENANT-ID`; verify it resolves only to the caller's permitted tenant.
