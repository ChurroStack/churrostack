# Fix MCP tokens never carrying the `mcp` audience

**Date:** 2026-09-22  
**Area:** `apps/api` OpenIddict server (`Program.cs`), `Utils/MigrationExtension.cs`

## Trigger

A background `/code-review high` run on the MCP server diff flagged, as its
top finding: every `/mcp` request would 403, unconditionally, for every
caller — making the whole feature non-functional regardless of the
authorization/consent work layered on top of it.

## Root cause

`Program.cs`'s `AddServer(...)` block calls `options.RegisterResources(...)`
and `options.RegisterScopes("mcp")`. A comment next to those calls asserted
that registering the `mcp` scope this way was "what makes OpenIddict stamp
`aud=<mcpResource>` onto tokens requested with `scope=mcp`" — that assertion
was wrong. `RegisterResources`/`RegisterScopes` only configure **server
options**: what `scopes_supported` advertises in discovery, and what a
`resource`/`scope` request parameter is allowed to ask for. They do not
create a row in the scope store.

The actual audience stamping happens in `OAuthController` via
`identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()))`
— `IOpenIddictScopeManager.ListResourcesAsync` is purely store-backed and has
no knowledge of `OpenIddictServerOptions.Scopes`. With no `mcp` scope row in
the database, `ListResourcesAsync` resolves to an empty set for any request
scoped to `mcp`, so no `resources` (and therefore no `aud`/`oi_aud` claim) is
ever attached to the issued token. `McpPolicy`'s
`RequireClaim(Claims.Private.Audience, mcpResource.AbsoluteUri)` then fails
every single time, because the claim doesn't exist.

This had never been exercised end-to-end before the review: the app's other
two clients (`api`, `app`) both have their scope rows seeded explicitly in
`MigrationExtension.RegisterApplications`, so the gap was specific to the new
`mcp` scope and nothing else in the codebase hit the same path.

## Fix

Seed a real `mcp` scope row in `MigrationExtension.RegisterApplications`,
following the exact pattern `RegisterAPIApplication` already uses for the
`api/.default` scope — a raw `OpenIdScope` entity with `Resources` as a
JSON-serialized array, not an `OpenIddictScopeDescriptor`:

```csharp
if (await scopeManager.FindByNameAsync("mcp") is null)
{
    await scopeManager.CreateAsync(new OpenIdScope
    {
        Name = "mcp",
        DisplayName = LocalizationService.GetString("ChurroStack MCP access"),
        Description = LocalizationService.GetString("Read your environments, applications and LLM configuration."),
        Resources = JsonSerializer.Serialize(new[] { mcpResource }, JsonSettings.Value)
    });
}
```

Also fixed a related, separately-flagged issuer mismatch in the same
metadata block: `Program.cs` set `config.Issuer` from `new Uri(BaseUrl)` (via
`options.Configure`), while the MCP `ResourceMetadata.AuthorizationServers`
entry was built from `BaseUrl.TrimEnd('/')`. Discovery always publishes
`Issuer.AbsoluteUri`, which for a host-only URI carries a trailing slash —
so the two values were deterministically one character apart, byte-inexact
for any client doing a strict RFC 8414 §3.3 issuer comparison. Both values
now derive from one shared `Uri` instance (`issuerUri`) so they can't drift.

Also added `Program.GetMcpResource(IConfiguration)` as the single source of
truth for the MCP resource identifier — it was previously computed three
times independently (`Program.cs`, `OAuthController.Register`, and now the
new scope-seeding code), each a `new Uri(new Uri(BaseUrl), "mcp")` expression
that could silently drift from the others.

Deleted the incorrect comment in `Program.cs` that claimed `RegisterScopes`
performs the stamping.

The seed is also kept in sync on every boot, not just written once: if the
`mcp` scope row already exists but its `Resources` isn't *exactly* the
current single-element `GetMcpResource(configuration)` value (e.g. `BaseUrl`
changed after the row was first seeded — a custom domain, an environment
promotion), the row is updated rather than left stale. Exact match, not "does
it contain the current value" — a row holding the current resource plus a
stale leftover one would pass a looser check untouched and then stamp both
onto every issued token's audience. A stale/multi-valued row here has no
other symptom than every `/mcp` request 403ing (or behaving unpredictably)
with no diagnostic pointing at the cause, so this was worth closing off
rather than leaving as a known gap.

## Verification

1. `dotnet build src/ChurrOS.slnx` and `dotnet publish -p:UseAppHost=false`:
   both succeeded, 0 errors.
2. `dotnet test src/ChurrOS.slnx`: 44 passed, 0 failed.
3. Live (Docker deps + API, `BaseUrl=https://localhost:5080/`): confirmed via
   the boot log that the `mcp` scope row is inserted with
   `resources: ["https://localhost:5080/mcp"]`, matching `GetMcpResource`
   exactly. `curl /.well-known/oauth-authorization-server`: `issuer` is
   `https://localhost:5080/`; `curl /.well-known/oauth-protected-resource/mcp`:
   `authorization_servers` is `["https://localhost:5080/"]` — byte-identical,
   confirming the issuer-mismatch fix independently of the scope fix.
4. **Not verified live, and could not be from this environment:** the actual
   `oi_aud`/`oi_scp` claims on an issued access token, and a `200` from
   `GET /mcp` with it. Both require a completed authorization — the flow
   stops at the redirect to `/oauth/login`, which challenges the
   `external.microsoft` scheme, unregistered in this environment because no
   real Microsoft OAuth app credentials are configured (`Program.cs` only
   registers that scheme when `ExternalProviders:Microsoft:ClientId` is
   set). This is the same limitation the 2026-09-21 redirect-uri postmortem
   flagged for its own manual PWA check — **outstanding**, requires a real
   deployment with live Microsoft IdP credentials.
