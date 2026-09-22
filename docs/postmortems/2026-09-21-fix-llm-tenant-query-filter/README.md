# Fix missing tenant query filter on `Llm`

**Date:** 2026-09-21  
**Area:** `apps/api` data layer (`ChurrosDbContext`), LLM listing endpoints

## Trigger

While building an MCP tool (`list_llms`) that reuses the existing `GetLlms`
command, we found that an administrator's request returns LLM rows from every
account, not just their own — including `Destination[].ApiKey` secrets. The
API's own `GET /api/llms` has the same defect; it had gone unnoticed because
the admin path is the only one that omits its own ACL filter.

## Root cause

`ChurrosDbContext.OnModelCreating` declares a global
`HasQueryFilter(mt => mt.AccountId == AccountId)` for every tenant-scoped
entity — `Acl`, `Application`, `Domain.Environment`, `Identity`, `Template`,
etc. — but `Llm` was never added to that list.

Non-admin callers were incidentally protected: `GetLlms.Handler`'s non-admin
branch filters by ACL ids returned from `GetIdentityAcls`, which itself scopes
its raw SQL by `account_id`. The **admin** branch applies no `Where` clause at
all (admins are assumed to see everything within their own tenant), so with no
query filter in place it returned every tenant's `Llm` rows unfiltered.

## Fix

- Added `modelBuilder.Entity<Llm>().HasQueryFilter(mt => mt.AccountId == AccountId);`
  next to the other tenant filters in `ChurrosDbContext.OnModelCreating`. No
  migration was needed — query filters are a query-time concern, not part of
  the database schema.
- Added a regression test (`ChurrosDbContextLlmTenantFilterTests`) that seeds
  `Llm` rows for two accounts via `ChurrosDbContext.Set<Llm>()` directly (no
  `Where` clause) and asserts each tenant's context only ever returns its own
  rows.

## Verification

1. `dotnet build src/ChurrOS.slnx`: succeeded, 0 errors.
2. Reverted the filter, reran the new test — it failed, returning both
   accounts' rows (`Assert.Single() Failure: The collection contained 2
   items`), confirming the test catches the regression it targets.
3. Restored the filter, reran the test — passed.
4. `dotnet test src/ChurrOS.slnx --filter "FullyQualifiedName~ChurrosDbContextLlmTenantFilterTests"`:
   1 passed, 0 failed.
