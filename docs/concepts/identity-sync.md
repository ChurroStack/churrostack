# Identity sync (Microsoft Entra)

Vocabulary and contracts for the **Sync from Microsoft Entra** feature (Settings → Identities → ⋮). Code lives in
`apps/api/src/ChurrOS.Api/Services/Entra/` (runner, planner, Graph client), `Jobs/EntraSyncJob.cs`,
`Controllers/IdentitySyncController.cs` and `apps/ui/src/pages/settings/dialogs/entra-sync.tsx`.

## Vocabulary

| Term | Meaning |
|---|---|
| **Source** (`identity.source`, `identity_member.source`) | `local` = managed by admins (UI / xlsx import); `entra` = created/owned by the sync. |
| **Linked** | A `local` identity whose name matched an Entra object: `external_id` is set, display name and Entra memberships are kept in sync, but it keeps its role and is **never disabled or deleted** by the sync. Shown with the `Entra` badge. |
| **Disabled** | `identity.lock_after <= now`. `AccountMembershipResolver` (used by `MultiTenantMiddleware`) rejects the identity with 403. Only `source=entra` users are disabled by the sync. |
| **Entra membership** | An `identity_member` row with `source=entra`. Only these rows are ever removed by the sync; admin-added (`local`) memberships always survive, even if Entra does not list them. A row that already exists as `local` stays `local`. |

## Rules

- **Names:** user = `(mail ?? userPrincipalName).toLowerInvariant()` — the same expression the Microsoft login uses, so a synced user matches their login. Group = Entra `displayName`, lowercased. Duplicates and collisions with a different identity type are skipped with a warning.
- **Never delete users.** Deleting an identity cascades to everything it created (applications, environments, API keys…). Users removed from Entra, disabled in Entra (`accountEnabled=false`) or no longer matching the filter are **disabled** and lose their Entra memberships; if they come back they are re-enabled. `source=entra` groups are deleted **only when they no longer exist in Entra** (deleting a group cascades its ACL grants); a group that is merely filtered out or empty is kept and just loses its Entra memberships. Users skipped by a data-quality warning (no mail, duplicate name) are never disabled for it.
- **New users** get `Role=User` and join the built-in `users` group. The role of existing identities is never changed. The `users` group is never created, renamed or deleted by the sync.
- **Filter** (optional) evaluates *users*: `(prop1 = A or B) AND (prop2 = C or D)`, case-insensitive, matched in memory against the `$select`ed Graph properties. A group is synced only if it has ≥1 matching transitive member and receives only matching members.
- **Group members** come from `/groups/{id}/transitiveMembers/microsoft.graph.user` (paged, nested groups flattened). `$expand=members` is not used: Graph caps it at 20 items.
- **Safety guard:** if 0 users match while `source=entra` users exist, the run fails instead of disabling everyone (wrong tenant / filter typo).
- **Atomicity:** fetching is read-only and cancellable; changes are applied in one transaction at the end (not cancellable).

## Configuration

- Per account, in `Account.Metadata["entraSync"]`: `{ enabled, cronExpression (5-field, UTC), filter: { enabled, rules: [{ property, values[] }] } }`.
- `ClientId` / `ClientSecret` are server config (`ExternalProviders:Microsoft:*`; OAuth client-credentials against `https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token`).
- **Tenant:** `Account.Metadata["entraTenantId"]` — set by the operator directly in the account entry — wins; if absent, the server default `ExternalProviders:Microsoft:TenantId` is used. A malformed account value is a misconfiguration (sync unavailable), not a fallback. The tenant is **never taken from the request or editable in the UI**: the app secret is shared by the whole installation, so a user-supplied tenant would let an account admin pull another organisation's directory (if it had consented to the app) into their account. Note that accounts without their own `entraTenantId` all share the default tenant, so on an installation hosting unrelated accounts set `entraTenantId` on every account and leave the default empty.
- The app registration needs **application** permissions `User.Read.All` and `GroupMember.Read.All` with admin consent (the login only needs delegated `User.Read`).

## Cache / state contract

| Key | Writer | Reader | TTL | Invalidation |
|---|---|---|---|---|
| `churros_tenant:{accountId}:entra_sync:status` | Start (`queued`), `EntraSyncService` (each page/group, end) | `GET …/status` (UI polls), Start (409 check) | 30 days | Overwritten by each run. A queued/running status with a heartbeat older than 5 min is reported as `failed` ("interrupted"). |
| `churros_tenant:{accountId}:entra_sync:cancel` | Stop | `EntraSyncService` between pages/groups | 1 h | Cleared by Start, at the start of scheduled runs, and at the end of every run. |
| `tenant:{accountId}:account` (existing) | `ChurrosDbContext.GetTenant()` | quota/domain readers | 1 h | Evicted when the sync config is saved. The sync itself reads config straight from the DB. |
| Identity / authorization caches (existing) | existing code | existing code | existing | Invalidated for every created/changed/deleted identity and the members of affected groups after the apply step. |

## API

`GET|PUT /api/identities/sync/entra/config`, `GET …/status`, `POST …/start` (409 if running), `POST …/stop`. Admin only.
Stop works across a Quartz cluster because it is a Redis flag, not `IScheduler.Interrupt` (local node only).
