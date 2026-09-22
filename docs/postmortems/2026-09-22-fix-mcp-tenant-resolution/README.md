# Fix `/mcp` (and any bearer-token call) 403ing for multi-account identities

**Date:** 2026-09-22  
**Area:** `apps/api` (`Middlewares/MultiTenantMiddleware.cs`)

## Trigger

A background `/code-review high` run on the MCP server diff flagged that any
identity belonging to more than one account would get a blanket 403 on
every `/mcp` request, regardless of whether the OAuth token itself was
valid.

## Root cause

`MultiTenantMiddleware` resolves the caller's tenant for every authenticated
request by reading an `X-TENANT-ID` header; if the header is absent, it asks
`AccountMembershipResolver.ResolveAccountIdAsync` to pick a default, which
only succeeds when the identity belongs to **exactly one** account —
otherwise it returns `null` and the middleware responds `403 Forbidden`
before the request reaches routing.

MCP clients have no mechanism to send a custom `X-TENANT-ID` header (nothing
in the MCP or OAuth spec provides for one), so every MCP request arrives
with no header at all. For any identity in more than one account, this was
an unconditional 403 regardless of token validity or scope.

This is not new to MCP: grepping the UI confirms `X-TENANT-ID` is never sent
by `apps/ui` either — only referenced in this middleware's own test file.
The PWA has simply never been exercised by a multi-account identity in
practice. MCP is what first turned a pre-existing, latent gap into a
guaranteed-reproducible failure, because it has no path to the header at
all.

## Fix

`OAuthController.Exchange()` already sets a `"tid"` claim on every token it
issues (resolved at token-issuance time, via `ResolveDefaultTenat`). Fall
back to that claim in `MultiTenantMiddleware` when the header is absent,
before defaulting to "pick the identity's only account":

```csharp
if (!requestedAccountId.HasValue)
{
    var tidClaim = httpContext.User.FindFirst("tid")?.Value;
    if (long.TryParse(tidClaim, out var tidAccountId))
    {
        requestedAccountId = tidAccountId;
    }
}
```

`ResolveAccountIdAsync` still re-verifies membership against the database
for whatever id it's given, so this doesn't bypass authorization — it only
supplies a better default request when no explicit header is present. The
fix is not MCP-specific: it applies to every bearer-token request from a
multi-account identity, closing the same latent gap for any future
non-browser API client, not just this one.

## Verification

1. `dotnet build src/ChurrOS.slnx`: succeeded, 0 errors.
2. `dotnet test src/ChurrOS.slnx`: 46 passed, 0 failed — the existing
   `MultiTenantMiddlewareTests` (header-based resolution) pass unchanged,
   plus two new cases: falls back to the `tid` claim when no header is
   present, and the header still wins when both are present.
3. **Not verified live, and could not be from this environment:** a
   multi-account identity's `GET /mcp` call actually returning `200`. That
   requires a completed OAuth login, which needs real Microsoft IdP
   credentials this environment doesn't have (see the same caveat in
   `docs/postmortems/2026-09-22-fix-mcp-scope-audience`) —
   **outstanding**, requires a real deployment.
