# Fix findings from `/code-review` on the consent-screen diff

**Date:** 2026-09-22  
**Area:** `apps/api` OpenIddict server (`OAuthController.cs`, `Program.cs`, `MultiTenantMiddleware.cs`), `apps/ui/nginx.conf`

## Trigger

A `/code-review` run on the full consent-screen diff (see
[`docs/postmortems/2026-09-22-fix-mcp-scope-audience`](../2026-09-22-fix-mcp-scope-audience/README.md)
and its siblings for the feature this reviews) returned ten findings. This
covers the six acted on; the other four are noted as deliberately deferred
at the end.

## Findings and fixes

**1. (Critical) The new `api`/`app` same-origin redirect_uri checks trust a
spoofable `Host`.** `Program.cs`'s `ForwardedHeadersOptions` enables
`XForwardedHost` with `KnownProxies`/`KnownNetworks` cleared — a documented
"trust any upstream" configuration, kept that way because the actual
container/k8s proxy address isn't static. `nginx.conf` never set or
stripped `X-Forwarded-Host`, so a client-supplied value passed straight
through to Kestrel, which honored it. `IsApiRedirectUri`/`IsAppRedirectUri`
(added earlier today) compare against exactly that value — so a request
carrying `X-Forwarded-Host: evil.example` alongside a matching
`redirect_uri` defeated the same-origin check entirely, delivering an
authorization code to an attacker-controlled origin. Fixed at the actual
trust boundary: `nginx.conf` now always sends
`proxy_set_header X-Forwarded-Host $host;`, which *replaces* rather than
merges, so a client-supplied value can never reach the API. The app's
`ForwardedHeadersOptions` was deliberately left untouched — narrowing
`KnownProxies` would need a static proxy address this deployment doesn't
have.

**2. Two consent-form POST actions could trigger an unhandled 500.**
`Accept()`/`Deny()` are both `[HttpPost("authorize")]`, disambiguated only
by `FormValueRequiredAttribute` checking for `submit.Accept`/`submit.Deny`
independently. A form body carrying both fields non-empty (the real
`Consent.cshtml` form never does, but nothing stopped an adversarial or
malformed POST) made both actions' selector match, and ASP.NET Core's
action selector throws `AmbiguousMatchException` for two equally-valid
candidates on one route. Fixed by giving `FormValueRequiredAttribute` an
optional `excludedName` parameter: `Accept()` now also requires
`submit.Deny` absent and vice versa. With both present, neither action
matches, and the request falls through to the unconstrained `Authorize()`
action instead — a re-prompt, not a decision, and no crash.

**3. DCR clients could escalate scope at `/authorize`.**
`options.IgnoreScopePermissions()` (set when the MCP server was first
built) disables OpenIddict's own scope-permission check everywhere,
including `/authorize`. The only replacement checks the token endpoint,
and only when the token request explicitly resends a `scope` parameter —
which a standard `authorization_code` exchange doesn't. Nothing was
checking, at `/authorize`, that a client actually held a `scp:` permission
for what it requested. Opening DCR today (see the sibling postmortems) is
what turned this from a narrow, pre-existing gap into one reachable by any
self-registered public client: a DCR client granted only `scp:mcp` could
request `scope=mcp api/.default`, and on one Allow click receive a token
whose audience covers the general REST API. Fixed by adding a
scope-permission check in `HandleAuthorizeAsync`, extracted as
`OAuthController.GetDeniedScopes` for testability, applied to every client
except `api`/`app` (the token-endpoint validator already special-cases
`api`'s dynamic per-application scopes, which this check can't replicate
safely, and both are trusted first-party clients — not the threat this
closes).

**4. `Deny()`'s trace log always recorded an empty `sub`.** It reads
`HttpContext.User`, populated by the app's *default* authenticate scheme
(a JWT bearer scheme) — always unauthenticated for a browser form POST.
The signed-in user's identity lives on the external cookie instead, the
same one `HandleAuthorizeAsync` reads. Fixed by authenticating against
`IdentityConstants.ExternalScheme` in `Deny()` too, before logging.

**5. Fallback scope copy on the consent screen bypassed localization.**
`FallbackScopeDisplayName`/`FallbackScopeDescription` (added earlier today)
returned raw English literals, unlike every other string on the same page.
Fixed by routing both through `LocalizationService.GetString`, matching the
rest of the page and `apps/api/CLAUDE.md`'s convention.

**6. The `tid`-claim tenant-resolution fallback (added earlier today) had
no trace log.** Fixed by logging which signal (`header` / `tid_claim` /
`none`) was used, under the existing `[TenantResolution]` prefix
`AccountMembershipResolver` already uses.

## Deliberately deferred

- **`IsSameOriginRedirectUri` doesn't compare port**, so a second listener
  on the same host but a different port would pass. `HostString.Host`
  never carries a port, and nginx's `$host` doesn't preserve one either
  (see finding 1's fix), so there's no reliable signal to compare against
  without broader changes to that trust chain — documented as a known gap
  at the call site rather than guessed at.
- **The local-dev-only Aspire/YARP ingress** (`AppHost.cs`) has the same
  unverified-`X-Forwarded-Host` shape as nginx did. Not fixed: it's
  localhost-only tooling, a materially smaller threat model, and the
  production path (nginx) is what needed closing.
- **`"api"`/`"app"` client-id literals duplicated** across the
  redirect-uri and scope-permission switches — real maintainability risk,
  not a live bug; left as-is rather than restructuring both checks in this
  pass.
- **Sequential per-scope store lookups** in `BuildConsentViewModelAsync` —
  minor latency for a page the user is already waiting on, negligible for
  the small scope counts this app actually issues.

## Verification

1. `dotnet build src/ChurrOS.slnx`: succeeded, 0 errors.
2. `dotnet test src/ChurrOS.slnx`: 49 passed, 0 failed — 3 new
   `GetDeniedScopes` cases, `MultiTenantMiddleware` tests updated for the
   new `ILogger` parameter.
3. Live (Docker deps + API): re-ran the full DCR acceptance matrix and the
   rate-limit check from the earlier verification pass — unchanged
   behavior, confirming findings 2–6 introduced no regression in the paths
   that don't require a real Microsoft login.
4. **Not verified live, and could not be from this environment:** finding
   1's fix (need a request through actual nginx with a spoofed header) and
   finding 3's check (unreachable without a completed OAuth login) —
   **outstanding**, requires a real deployment. Same caveat as the sibling
   postmortems for this feature.
