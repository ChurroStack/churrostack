# Fix disabled OAuth `redirect_uri` validation

**Date:** 2026-09-21  
**Area:** `apps/api` OpenIddict authorization server (`Program.cs`, `oauth/authorize`)

## Trigger

While adding an MCP server that needs Dynamic Client Registration (RFC 7591),
we found that OpenIddict's `redirect_uri` validation was completely disabled:
any syntactically-present `redirect_uri` was accepted for any client,
including the confidential `api` client. Opening client registration to
arbitrary DCR-registered clients on top of that would turn it into an
authorization-code-harvesting vector — an attacker registers a client, then
supplies any `redirect_uri` at authorize time and the server hands the code
straight to it.

## Root cause

`Program.cs` removed OpenIddict's stock `ValidateClientRedirectUri` handler
and replaced it with a handler that only rejected a **missing** `redirect_uri`
when the request's scope included `openid` — it never compared the supplied
value against anything registered per client.

Traced why: the `app` PWA client (`Utils/MigrationExtension.cs`,
`RegisterPwaNativeApplication`) is seeded with **zero** `RedirectUris`,
because `apps/ui`'s OIDC client (`oidc-spa`) derives its redirect from
`window.location.origin` at runtime — a value that varies by dev port,
staging domain, and any per-tenant custom production domain. OpenIddict's
stock validator does exact string matching against registered URIs, so it
would have rejected every one of those; disabling the check wholesale was the
workaround. (Ruled out the `/share/{app}/{port}` proxy pattern as the cause:
that path is Cookie-authenticated via a co-issued session cookie, and its one
first-party OIDC hop always uses a fixed `client_id=api` +
`redirect_uri=<BaseUrl>/login/signin-oidc` — it never varies.)

## Fix

Replaced the blanket bypass with three client-type-specific rules in the same
`ValidateAuthorizationRequestContext` handler:
- **`api`** (confidential, used only for its one fixed self-loopback OIDC
  callback): exact match against `<BaseUrl>/login/signin-oidc`, computed from
  the same `BaseUrl` config key already used to set the OpenIddict issuer.
- **`app`** (PWA, public, no fixed host set): same-origin check —
  `redirect_uri.Scheme == request.Scheme && redirect_uri.Host == request.Host`.
  This works because `nginx.conf` always serves the SPA and the API from one
  origin, and mirrors the trust boundary `LoginController.SignIn` already uses
  via `Url.IsLocalUrl(returnUrl)`. Extracted as `Program.IsSameOriginRedirectUri`
  for unit testing.
- **Every other client** (DCR-registered public clients, e.g. MCP clients):
  match a *registered* redirect URI on scheme, host, and path; if the
  *registered* host is a loopback address (`127.0.0.1`, `::1`, `localhost`),
  the port is ignored (OAuth 2.1 / RFC 8252 §7.3 — native/CLI clients can't
  predict their ephemeral local port). The exemption is gated on the
  registered host, not the incoming one, so a client can't claim it by
  registering a non-loopback host. Extracted as
  `Program.IsRegisteredLoopbackAwareRedirectUriAsync` for unit testing.

Considered validating the `app` client against each account's `Domains`
instead of same-origin. Rejected: at
`ValidateAuthorizationRequestContext` time the request isn't authenticated
yet, so `MultiTenantMiddleware` hasn't run and there's no resolved tenant to
look `Domains` up against. Same-origin needs no DB lookup, no config key, and
already covers custom domains since they're always fronted by the same nginx
instance that serves the API.

> **Update 2026-09-22:** this fix's own verification missed a host other
> than `BaseUrl` for the `api` client and the exact-path requirement for the
> `app` client — see
> [`docs/postmortems/2026-09-22-fix-redirect-uri-regressions`](../2026-09-22-fix-redirect-uri-regressions/README.md)
> for the two regressions a subsequent code review caught and the fixes.

## Verification

1. `dotnet build src/ChurrOS.slnx`: succeeded, 0 errors.
2. Added `RedirectUriValidationTests` (8 cases): same-origin accept/reject for
   the `app` rule (matching scheme+host; different scheme; different host);
   loopback-registered accepts a different port; non-loopback-registered
   rejects a different port; a loopback *incoming* URI is still rejected when
   the *registered* URI is not loopback; unknown client id rejected.
3. `dotnet test src/ChurrOS.slnx --filter "FullyQualifiedName~RedirectUriValidationTests"`:
   8 passed, 0 failed.
4. **Regression check (manual, required before merge):** sign into the `app`
   PWA end-to-end against a running instance — this is the one path this fix
   can break, since the PWA client has no registered `RedirectUris` and now
   depends entirely on the same-origin rule.
