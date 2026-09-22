# Fix regressions in the OAuth `redirect_uri` validation added the day before

**Date:** 2026-09-22  
**Area:** `apps/api` OpenIddict server (`Program.cs`, `oauth/authorize`)

See also: [`docs/postmortems/2026-09-21-fix-oauth-redirect-uri-validation`](../2026-09-21-fix-oauth-redirect-uri-validation/README.md),
the postmortem for the fix these regressions were introduced by, one day
earlier in the same branch.

## Trigger

A background `/code-review high` run on the branch (run while adding the
OAuth consent screen) flagged three defects in the previous day's
`redirect_uri` validation fix, plus a fourth in its own test suite that
masked one of them.

## Root causes and fixes

**1. `api` client login broke on any host other than the configured
`BaseUrl`.** The previous fix matched `redirect_uri` for the `api` client
by exact string equality against a value built once from configuration
(`new Uri(new Uri(BaseUrl), "login/signin-oidc").AbsoluteUri`). But
`Microsoft.AspNetCore.Authentication.OpenIdConnect`'s handler builds the
`redirect_uri` it actually sends from the **live request's** scheme and
host — which only equals the configured `BaseUrl` when the app happens to
be reached through that exact host. Any deployment reachable via more than
one hostname (a raw IP during setup, a secondary domain, `localhost` during
local dev against a `BaseUrl` pointing elsewhere) would have every OIDC
login attempt rejected. Fixed by matching same-origin (like the `app`
client) plus the fixed path `/login/signin-oidc`, instead of a
`BaseUrl`-derived literal — extracted as `Program.IsApiRedirectUri`.

**2. The `app` client's same-origin check enabled authorization-code
interception.** The previous fix validated only scheme and host for the
`app` client, not path — so any same-origin path was a valid redirect
target, including tenant-controlled content served under `/share/{app}/{port}`.
An attacker able to place content there (or simply misuse the proxy) could
receive the authorization code themselves. Verified directly against
`oidc-spa` 8.7.13's source (`core/homeAndRedirectUri.js` →
`toFullyQualifiedUrl`): the redirect URI it actually sends is always
`window.location.origin + BASE_URL`, never the current page's path, and this
app's `vite.config.ts` sets `BASE_URL` to `/` — so the PWA's redirect URI is
always exactly `"<origin>/"`, one fixed value, not an open set. Fixed by
additionally requiring the path to equal `/` exactly — extracted as
`Program.IsAppRedirectUri`. (The `app` client is `ConsentType.Implicit`, so
this was a real gap the consent screen added in the same PR would not have
caught on its own.)

**3. IPv6 loopback clients were rejected at DCR registration, and the
loopback any-port exemption silently failed for them.** `Uri.Host` renders
an IPv6 literal in bracket notation (`"[::1]"`), which never matched the
bracket-free `"::1"` entry in `Program.LoopbackHosts`. The DCR rejection
message told the caller `"::1"` was an acceptable host while the actual
comparison could never succeed for it. Fixed by adding
`Program.IsLoopbackHost(string)`, which strips brackets before comparing,
and routing every direct `LoopbackHosts.Contains` call site through it.

**4. A same-origin unit test asserted a runtime-impossible input, which is
what let #1 and #2 ship unnoticed.** `Program.cs` unconditionally forces
`context.Request.Scheme = "https"` before OpenIddict's pipeline runs (a
defence-in-depth line, unrelated to and predating this work), so the
`httpRequest.Scheme` the redirect-uri handler sees is never `"http"` at
runtime — yet a test case fed `requestScheme: "http"` and asserted a
same-origin match. Fixed by dropping the request-scheme comparison
entirely: `IsSameOriginRedirectUri` now checks the **redirect_uri's own**
scheme (must be `https`, unless the host is loopback) rather than comparing
against the unreliable forced-https request scheme, and the test matrix was
rewritten (not extended) to match.

## Verification

1. `dotnet build src/ChurrOS.slnx` and `dotnet publish -p:UseAppHost=false`:
   both succeeded, 0 errors.
2. `dotnet test src/ChurrOS.slnx`: 44 passed, 0 failed, including a
   rewritten `RedirectUriValidationTests` (same-origin+https-unless-loopback,
   `api` accepted on a non-`BaseUrl` host, `app` accepted only at path `/`
   and rejected at `/share/...`, IPv6-bracket loopback normalization).
3. **Regression — PWA login**, on a host that is *not* the configured
   `BaseUrl`: signed in end-to-end; this is the exact case #1 broke.
4. **Regression — CLI**, IPv6 loopback: `POST /oauth/register` with
   `http://[::1]:<port>/callback` → `201` (was rejected before this fix).
