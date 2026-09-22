using ChurrOS.Api.Domain.Auth;
using ChurrOS.Api.Models.Dtos.OAuth;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.AspNet;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Collections.Immutable;
using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ChurrOS.Api.Controllers
{
    [ApiController]
    [Route("oauth")]
    public class OAuthController : Controller
    {
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly IOpenIddictAuthorizationManager _authorizationManager;
        private readonly IOpenIddictScopeManager _scopeManager;
        private readonly SignInManager<OpenIdUser> _signInManager;
        private readonly UserManager<OpenIdUser> _userManager;
        private readonly ICacheService _cacheService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuthController> _logger;

        public OAuthController(
            IOpenIddictApplicationManager applicationManager,
            IOpenIddictAuthorizationManager authorizationManager,
            IOpenIddictScopeManager scopeManager,
            SignInManager<OpenIdUser> signInManager,
            UserManager<OpenIdUser> userManager,
            ICacheService cacheService,
            IConfiguration configuration,
            ILogger<OAuthController> logger)
        {
            _applicationManager = applicationManager;
            _authorizationManager = authorizationManager;
            _scopeManager = scopeManager;
            _signInManager = signInManager;
            _userManager = userManager;
            _cacheService = cacheService;
            _configuration = configuration;
            _logger = logger;
        }

        // Co-issues a CookieAuthenticationDefaults session alongside the OAuth tokens.
        // Required so `window.open('/share/{app}/{port}/')` (a top-level navigation
        // that cannot present an Authorization: Bearer header) is authenticated by
        // `AppCookiePolicy`. NameClaimType must match the JWT path (Program.cs:393)
        // so `Identity.Name` resolves to `preferred_username` in both flows.
        //
        // Claims policy: copies the full source identity so AspNet.Identity's
        // SecurityStamp survives — the Cookie scheme relies on it for
        // out-of-band revocation. The JWT path's GetDestinations filter does
        // NOT apply here; if a future identity gains a sensitive claim that
        // must not enter the cookie, scrub it explicitly before calling this
        // helper.
        //
        // Lifetime: AllowRefresh=false so SlidingExpiration on the Cookie
        // scheme does not extend the cookie indefinitely. The cookie's
        // 8 h absolute expiry is bounded by the explicit re-issue on every
        // Authorize/refresh-token Exchange, keeping it within JWT refresh
        // distance instead of drifting on idle /share/* traffic.
        private async Task IssueCookieSessionAsync(ClaimsIdentity sourceIdentity, string flow)
        {
            var cookieIdentity = new ClaimsIdentity(
                sourceIdentity.Claims,
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.PreferredUsername,
                roleType: Claims.Role);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(cookieIdentity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
                    AllowRefresh = false
                });

            _logger.LogDebug(
                "[oauth.cookie.signin] flow={Flow} sub={Sub} tid={Tid}",
                flow,
                cookieIdentity.FindFirst(Claims.Subject)?.Value,
                cookieIdentity.FindFirst("tid")?.Value);
        }

        // Coarse heuristic for "this token request came from a browser".
        // Used to skip the co-issued cookie on CLI / service-to-service
        // refresh-token calls. Any incoming cookie at all (including
        // session/Identity cookies set during the original interactive
        // login) means it's worth co-issuing the share-app session cookie.
        private bool RequestLooksBrowserDriven() => HttpContext.Request.Cookies.Count > 0;

        // RFC 7591 Dynamic Client Registration -- public clients only. OpenIddict has no built-in
        // DCR support (tracked upstream in openiddict-core#2404, unimplemented even in the 8.0
        // previews; the ABP framework hit this exact requirement for the exact same reason -- an
        // MCP server needing Claude/MCP-client registration -- and reached the same conclusion:
        // github.com/abpframework/abp/issues/24193). Anonymous by design, matching every other
        // action in this controller (Login/Authorize/Exchange) -- there is no ambient [Authorize]
        // on this controller or a global fallback policy. Registered clients get
        // ConsentType.Explicit: Authorize() now shows a consent screen (see HandleAuthorizeAsync /
        // RequiresConsentPrompt) before ever issuing a code to a DCR-registered client, on every
        // grant (loopback CLI clients included -- a loopback redirect proves nothing, any local
        // process can bind a port and claim to be a trusted client).
        //
        // SECURITY: any https redirect_uri is accepted (the posture most public MCP servers run);
        // http is accepted only for a loopback host (127.0.0.1 / ::1 / localhost), per RFC 8252 §7.3
        // and the MCP spec's "redirect URIs MUST be either localhost or use HTTPS". This is safe
        // specifically *because* every DCR client now gets ConsentType.Explicit above: registering a
        // client with an attacker-controlled https redirect_uri no longer lets an attacker silently
        // collect an authorization code -- Authorize() stops and shows the signed-in user exactly
        // which client is asking and which host the response goes to (Views/OAuth/Consent.cshtml)
        // before anything is issued, and a "Deny" ends the flow with no code granted.
        [HttpPost("register")]
        [Consumes(MediaTypeNames.Application.Json)]
        [Produces(MediaTypeNames.Application.Json)]
        [EnableRateLimiting("dcr")]
        public async Task<IActionResult> Register([FromBody] DcrRegisterRequest request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.TokenEndpointAuthMethod) &&
                !string.Equals(request.TokenEndpointAuthMethod, "none", StringComparison.Ordinal))
            {
                return DcrError("invalid_client_metadata", "Only the \"none\" (public client) token_endpoint_auth_method is supported.");
            }

            if (request.RedirectUris is not { Length: > 0 })
            {
                return DcrError("invalid_redirect_uri", "At least one redirect_uri is required.");
            }

            if (!IsValidDcrClientName(request.ClientName))
            {
                // client_name is rendered verbatim on the consent screen (Razor-encoded, so this
                // isn't an XSS concern) -- this is about not letting an attacker-chosen string
                // deface or truncate that page's layout.
                return DcrError("invalid_client_metadata", "client_name must be 200 characters or fewer and contain no control characters.");
            }

            foreach (var raw in request.RedirectUris)
            {
                var (isValidRedirectUri, error) = ValidateDcrRedirectUri(raw);
                if (!isValidRedirectUri)
                {
                    if (string.Equals(error, DcrHttpNonLoopbackError, StringComparison.Ordinal))
                    {
                        _logger.LogWarning(
                            "[oauth.dcr.reject] reason=http_non_loopback_redirect_uri clientName={ClientName} raw={RawRedirectUri}",
                            request.ClientName, raw);
                    }

                    return DcrError("invalid_redirect_uri", $"'{raw}' {error}");
                }
            }

            var grantTypes = request.GrantTypes is { Length: > 0 } ? request.GrantTypes : [GrantTypes.AuthorizationCode];
            var allowedGrantTypes = new HashSet<string> { GrantTypes.AuthorizationCode, GrantTypes.RefreshToken };
            if (grantTypes.Any(grantType => !allowedGrantTypes.Contains(grantType)))
            {
                return DcrError("invalid_client_metadata", "Only the authorization_code and refresh_token grant types are supported.");
            }

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = $"mcp_{Guid.NewGuid():N}",
                ClientType = ClientTypes.Public,
                // Explicit, not Implicit: Authorize() now shows a consent screen for every
                // DCR-registered client before the first token is issued (see RequiresConsentPrompt
                // in HandleAuthorizeAsync / the class comment above).
                ConsentType = ConsentTypes.Explicit,
                DisplayName = string.IsNullOrWhiteSpace(request.ClientName) ? "MCP client" : request.ClientName,
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.ResponseTypes.Code,
                    // Required by the custom scope validator (see AddServer's
                    // ValidateTokenRequestContext handler), which checks this exact
                    // "scp:{scope}" permission string before allowing a client to request it.
                    Permissions.Prefixes.Scope + "mcp",
                    // OpenIddict's stock ValidateResourcePermissions handler (active by default;
                    // NOT bypassed by IgnoreScopePermissions, which only covers scopes) rejects
                    // any authorize/token request carrying a `resource` parameter unless the
                    // client holds this exact "rsrc:{resource}" permission -- confirmed
                    // empirically: without this, /oauth/authorize returns invalid_request
                    // "This client application is not allowed to use the specified resource(s)"
                    // (OpenIddict error ID2192) for every MCP client, since RFC 8707 `resource`
                    // is mandatory for MCP clients.
                    Permissions.Prefixes.Resource + Program.GetMcpResource(_configuration).AbsoluteUri,
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            };

            if (grantTypes.Contains(GrantTypes.RefreshToken))
            {
                descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            }

            foreach (var raw in request.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(raw));
            }

            await _applicationManager.CreateAsync(descriptor, cancellationToken);

            _logger.LogInformation(
                "[oauth.dcr.register] clientId={ClientId} clientName={ClientName} redirectHosts={RedirectHosts}",
                descriptor.ClientId, descriptor.DisplayName,
                string.Join(",", request.RedirectUris.Select(uri => new Uri(uri).Host)));

            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";

            return StatusCode(StatusCodes.Status201Created, new DcrRegisterResponse
            {
                ClientId = descriptor.ClientId,
                ClientIdIssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ClientName = descriptor.DisplayName,
                RedirectUris = request.RedirectUris,
                GrantTypes = [.. grantTypes],
                TokenEndpointAuthMethod = "none",
            });
        }

        private IActionResult DcrError(string error, string description)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Pragma = "no-cache";
            return BadRequest(new { error, error_description = description });
        }

        private const string DcrHttpNonLoopbackError =
            "uses http but is not a loopback host (127.0.0.1, ::1, or localhost). Non-loopback redirect_uris must use https.";

        // Any https redirect_uri is accepted; http only for a loopback host (127.0.0.1 / ::1 /
        // localhost), per RFC 8252 §7.3 and the MCP spec's "redirect URIs MUST be either localhost
        // or use HTTPS" -- see the class comment on Register() for why this is safe now that every
        // DCR client gets ConsentType.Explicit. A fragment is always rejected (OAuth forbids one in
        // a redirect URI).
        internal static bool IsValidDcrClientName(string? clientName)
            => string.IsNullOrEmpty(clientName) || (clientName.Length <= 200 && !clientName.Any(char.IsControl));

        internal static (bool IsValid, string? Error) ValidateDcrRedirectUri(string raw)
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var redirectUri) ||
                (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "is not a valid absolute http(s) URI.");
            }

            if (!string.IsNullOrEmpty(redirectUri.Fragment))
            {
                return (false, "must not contain a fragment.");
            }

            if (redirectUri.Scheme == Uri.UriSchemeHttp && !Program.IsLoopbackHost(redirectUri.Host))
            {
                return (false, DcrHttpNonLoopbackError);
            }

            return (true, null);
        }

        [HttpGet("login")]
        public IActionResult Login(string? redirectUri = null, string? prompt = null)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                redirectUri = _configuration["BaseUrl"]!.Trim('/');
            }
            else
            {
                redirectUri = Uri.UnescapeDataString(redirectUri);
            }
            if (Uri.IsWellFormedUriString(redirectUri, UriKind.Absolute))
            {
                redirectUri = redirectUri.Trim('/');
            }
            else
            {
                redirectUri = $"{_configuration["BaseUrl"]!.Trim('/')}/{redirectUri.Trim('/')}";
            }

            // External auth
            var provider = "external.microsoft";
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUri);
            properties.Parameters.Add("prompt", string.IsNullOrWhiteSpace(prompt) ? "select_account" : prompt);
            return new ChallengeResult(provider, properties);
        }

        [HttpGet("logout"), HttpPost("logout")]
        public async Task<IActionResult> LogoutPost()
        {
            // Ask ASP.NET Core Identity to delete the local and external cookies created
            // when the user agent is redirected from the external identity provider
            // after a successful authentication flow (e.g Google or Facebook).
            await _signInManager.SignOutAsync();

            // Delete cookie used during external authentication
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            // Delete the co-issued application session cookie minted by
            // Authorize/Exchange so logout clears /share/* access too.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Returning a SignOutResult will ask OpenIddict to redirect the user agent
            // to the post_logout_redirect_uri specified by the client application or to
            // the RedirectUri specified in the authentication properties if none was set.
            return SignOut(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = "/"
                });
        }

        [HttpPost("token"), Produces("application/json")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new BadHttpRequestException("The OpenID Connect request cannot be retrieved.");

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // Retrieve the claims principal stored in the authorization code/refresh token.
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // Retrieve the user profile corresponding to the authorization code/refresh token.
                var subject = result.Principal?.GetClaim(Claims.Subject);

                if (subject == null || result.Principal is null)
                    throw new BadHttpRequestException("Invalid principal object. No sub claim found.");

                var user = await _userManager.FindByIdAsync(subject);
                if (user is null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                        }));
                }

                // Ensure the user is still allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                        }));
                }

                var identity = new ClaimsIdentity(result.Principal.Claims,
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: Claims.PreferredUsername,
                    roleType: Claims.Role);

                var claims = await _userManager.GetClaimsAsync(user);

                // Override the user claims present in the principal in case they
                // changed since the authorization code/refresh token was issued.
                identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user))
                        .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user))
                        .SetClaim(Claims.Name, claims.FirstOrDefault(c => c.Type == Claims.Name)?.Value ?? await _userManager.GetUserNameAsync(user))
                        .SetClaim(Claims.PreferredUsername, await _userManager.GetUserNameAsync(user))
                        .SetClaims(Claims.Role, [.. (await _userManager.GetRolesAsync(user))]);
                identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

                // Try to resolve a valid accountId for user (either from state or picking a default one) 
                long.TryParse(request.State, out var accountId);
                accountId = ResolveDefaultTenat(identity.Name!, accountId) ?? 0;

                if (accountId <= 0)
                {
                    // Tenant not found, cannot issue token
                    throw new InvalidOperationException($"Cannot resolve a valid tenant for user '{identity.Name}'");
                }
                identity.SetClaim("tid", accountId.ToString());

                identity.SetDestinations(GetDestinations);

                // Refresh the co-issued cookie session so it tracks JWT refresh
                // lifetime instead of decaying independently. Skipped for the
                // client_credentials and token_exchange branches below — those
                // are not browser-driven. Also skipped when the refresh-token
                // request shows no sign of being browser-driven (CLI / service
                // calls), so we don't ship Set-Cookie to clients that can't use it.
                if (request.IsAuthorizationCodeGrantType() || RequestLooksBrowserDriven())
                {
                    await IssueCookieSessionAsync(
                        identity,
                        request.IsRefreshTokenGrantType() ? "refresh" : "authorization_code");
                }

                // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsClientCredentialsGrantType())
            {
                // Note: the client credentials are automatically validated by OpenIddict:
                // if client_id or client_secret are invalid, this action won't be invoked.

                var application = await _applicationManager.FindByClientIdAsync(request!.ClientId!) ??
                    throw new InvalidOperationException("The application cannot be found.");

                // Create a new ClaimsIdentity containing the claims that
                // will be used to create an id_token, a token or a code.
                var identity = new ClaimsIdentity(TokenValidationParameters.DefaultAuthenticationType, Claims.PreferredUsername, Claims.Role);

                // Use the client_id as the subject identifier.
                var clientId = await _applicationManager.GetClientIdAsync(application);
                identity.SetClaim(Claims.Subject, clientId);
                identity.SetClaim(Claims.Name, clientId);
                identity.SetClaim(Claims.PreferredUsername, clientId);
                identity.SetClaim(Claims.GivenName, await _applicationManager.GetDisplayNameAsync(application));
                var scopes = request.GetScopes();
                identity.SetScopes(scopes);
                identity.SetResources(await _scopeManager.ListResourcesAsync(scopes).ToListAsync());

                var properties = await _applicationManager.GetPropertiesAsync(application);
                if (properties != null && properties.TryGetValue("account_id", out var jsonAccountId))
                {
                    identity.SetClaim("tid", jsonAccountId.Deserialize<long>(JsonSettings.Value));
                }
                else
                {
                    // Try to resolve a valid accountId for application only if supplied by state
                    if (long.TryParse(request.State, out var accountId))
                    {
                        accountId = ResolveDefaultTenat(identity.Name!, accountId) ?? 0;
                        if (accountId <= 0)
                        {
                            // Tenant not found, cannot issue token
                            throw new InvalidOperationException($"Cannot resolve a valid tenant for user '{identity.Name}'");
                        }
                        identity.SetClaim("tid", accountId.ToString());
                    }
                }

                identity.SetDestinations(GetDestinations);

                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsTokenExchangeGrantType())
            {
                // Retrieve the claims principal stored in the subject token.
                // Note: the principal may not represent a user (e.g if the token was issued during a client credentials token
                // request and represents a client application): developers are strongly encouraged to ensure that the user
                // and client identifiers are randomly generated so that a malicious client cannot impersonate a legit user.
                // See https://datatracker.ietf.org/doc/html/rfc9068#SecurityConsiderations for more information.
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // If available, retrieve the claims principal stored in the actor token.
                var actor = result.Properties?.GetParameter<ClaimsPrincipal>(OpenIddictServerAspNetCoreConstants.Properties.ActorTokenPrincipal);

                // Retrieve the user profile corresponding to the subject token.
                var user = await _userManager.FindByIdAsync(result.Principal!.GetClaim(Claims.Subject)!);
                if (user is null)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                        }));
                }

                // Ensure the user is still allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                        }));
                }

                // Note: whether the identity represents a delegated or impersonated access (or any other
                // model) is entirely up to the implementer: to support all scenarios, OpenIddict doesn't
                // enforce any specific constraint on the identity used for the sign-in operation and only
                // requires that the standard "act" and "may_act" claims be valid JSON objects if present.
                var identity = new ClaimsIdentity(
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: Claims.PreferredUsername,
                    roleType: Claims.Role);

                var claims = await _userManager.GetClaimsAsync(user);

                // Add the claims that will be persisted in the issued token.
                identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user))
                        .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user))
                        .SetClaim(Claims.Name, claims.FirstOrDefault(c => c.Type == Claims.Name)?.Value ?? await _userManager.GetUserNameAsync(user))
                        .SetClaim(Claims.PreferredUsername, await _userManager.GetUserNameAsync(user))
                        .SetClaims(Claims.Role, [.. await _userManager.GetRolesAsync(user)]);

                var tidClaim = result.Principal?.Claims?.FirstOrDefault(o => o.Type == "tid");
                if (tidClaim is not null && !string.IsNullOrWhiteSpace(tidClaim.Value.ToString()))
                {
                    identity.SetClaim("tid", tidClaim.Value.ToString());
                }

                // Note: IdentityModel doesn't support serializing ClaimsIdentity.Actor to the
                // standard "act" claim yet, which requires adding the "act" claim manually.
                // For more information, see
                // https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/pull/3219.
                if (!string.IsNullOrEmpty(actor?.GetClaim(Claims.Subject)) &&
                    !string.Equals(identity.GetClaim(Claims.Subject), actor.GetClaim(Claims.Subject), StringComparison.Ordinal))
                {
                    identity.SetClaim(Claims.Actor, new JsonObject
                    {
                        [Claims.Subject] = actor.GetClaim(Claims.Subject)
                    });
                }

                // For that, simply restrict the list of scopes before calling SetScopes.
                identity.SetScopes(request.GetScopes());
                identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());
                identity.SetDestinations(GetDestinations);

                // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new InvalidOperationException("The specified grant type is not supported.");
        }

        [HttpGet("authorize")]
        [HttpPost("authorize")]
        [IgnoreAntiforgeryToken]
        public Task<IActionResult> Authorize() => HandleAuthorizeAsync(consentGranted: false);

        // The "Allow" half of the consent screen (Views/OAuth/Consent.cshtml posts here with
        // submit.Accept). FormValueRequired disambiguates this from the plain Authorize() action
        // above -- both are [HttpPost("authorize")] -- exactly the mechanism Deny() below already
        // used before this change. Re-enters HandleAuthorizeAsync rather than a bespoke endpoint so
        // OpenIddict re-validates the whole request (redirect_uri, PKCE challenge, resource, etc.)
        // from the hidden fields the consent form round-tripped, closing off parameter-swap between
        // the prompt and the approval.
        [HttpPost("authorize")]
        [FormValueRequired("submit.Accept", excludedName: "submit.Deny")]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Accept() => HandleAuthorizeAsync(consentGranted: true);

        private async Task<IActionResult> HandleAuthorizeAsync(bool consentGranted)
        {
            var request = HttpContext.GetOpenIddictServerRequest() ??
                throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // Retrieve the user principal stored in the authentication cookie.
            var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);

            if (result == null || !result.Succeeded || request.HasPromptValue(PromptValues.Login) ||
               (request.MaxAge != null && result.Properties?.IssuedUtc != null &&
                DateTimeOffset.UtcNow - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value)))
            {
                // If the client application requested promptless authentication,
                // return an error indicating that the user is not logged in.
                if (request.HasPromptValue(PromptValues.None))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                        }));
                }

                // Also strip "consent": if it survives, it reaches Microsoft's authorize endpoint via
                // Login() below and shows *their* consent screen instead of ours. The original
                // "consent" value still reaches this action again on the way back -- the whole
                // request (query/form) is round-tripped verbatim as the "redirectUri" parameter just
                // below -- so HandleAuthorizeAsync sees it via request.HasPromptValue(PromptValues.Consent).
                var prompt = string.Join(" ", request.GetPromptValues().Remove(PromptValues.Login).Remove(PromptValues.Consent));

                var parameters = new List<KeyValuePair<string, StringValues>>
                {
                    new KeyValuePair<string, StringValues>("redirectUri", new StringValues(Request.PathBase + Request.Path + QueryString.Create(Request.HasFormContentType ? Request.Form : Request.Query)))
                };

                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    parameters.Add(KeyValuePair.Create(Parameters.Prompt, new StringValues(prompt)));
                }

                return Redirect($"~/oauth/login{QueryString.Create(parameters)}");
            }

            var externalIdentity = result.Principal.Identity as ClaimsIdentity;
            string? displayName = externalIdentity!.FindFirst(ClaimTypes.Name)?.Value ?? externalIdentity.FindFirst(Claims.Name)?.Value;

            string? upn = null;
            if (string.IsNullOrWhiteSpace(upn))
                upn = externalIdentity?.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Upn)?.Value;
            if (string.IsNullOrWhiteSpace(upn))
                upn = externalIdentity?.Claims.FirstOrDefault(o => o.Type == ClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(upn))
                upn = externalIdentity?.Name;
            upn = upn?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(upn))
                throw new ArgumentException("Invalid UPN value");
            // Retrieve the profile of the logged in user.
            var user = await _userManager.FindByNameAsync(upn);
            if (user is null)
            {
                // TODO: Review autoenroll issue
                var createResult = await _userManager.CreateAsync(new OpenIdUser()
                {
                    Email = upn,
                    EmailConfirmed = true,
                    UserName = upn,
                });

                if (!createResult.Succeeded)
                    throw new InvalidOperationException("The user cannot be created.");

                user = await _userManager.FindByNameAsync(upn);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    await _userManager.AddClaimAsync(user!, new Claim(Claims.Name, displayName));
                }
            }

            // Retrieve the application details from the database.
            var application = await _applicationManager.FindByClientIdAsync(request!.ClientId!) ??
                throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

            // options.IgnoreScopePermissions() (AddServer, Program.cs) disables OpenIddict's stock
            // scope-permission check everywhere, including here at /authorize -- the only
            // replacement (Program.cs's ValidateTokenRequestContext handler) only runs at the token
            // endpoint, and only when the token request explicitly resends a non-empty `scope`
            // parameter, which a standard authorization_code exchange does not. Left unchecked here,
            // any DCR-registered client (granted only scp:mcp by OAuthController.Register) could
            // request scope=mcp api/.default at this endpoint and, on one Allow click, receive a
            // token whose audience covers the general REST API. Skips "api"/"app": the token-endpoint
            // validator already special-cases "api" (it's allowed to request per-application
            // "{appId}/..." scopes dynamically, which this check has no way to replicate safely), and
            // both are trusted first-party clients, not the DCR/public-client threat this closes.
            if (!string.Equals(request.ClientId, "api", StringComparison.Ordinal) &&
                !string.Equals(request.ClientId, "app", StringComparison.Ordinal))
            {
                var permissions = await _applicationManager.GetPermissionsAsync(application);
                var deniedScopes = GetDeniedScopes(permissions, request.GetScopes());

                if (deniedScopes.Length > 0)
                {
                    _logger.LogWarning(
                        "[oauth.authorize.reject] reason=scope_not_permitted clientId={ClientId} scopes={Scopes}",
                        request.ClientId, string.Join(" ", deniedScopes));

                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidScope,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                                $"The client application is not allowed to request the following scope(s): {string.Join(" ", deniedScopes)}."
                        }));
                }
            }

            // Retrieve the permanent authorizations associated with the user and the calling client application.
            var authorizations = await _authorizationManager.FindAsync(
                subject: await _userManager.GetUserIdAsync(user!),
                client: await _applicationManager.GetIdAsync(application),
                status: Statuses.Valid,
                type: AuthorizationTypes.Permanent,
                scopes: request.GetScopes()).ToListAsync();

            var consentType = await _applicationManager.GetConsentTypeAsync(application);
            if (RequiresConsentPrompt(consentType, consentGranted, authorizations.Count, request.HasPromptValue(PromptValues.Consent)))
            {
                // Systematic and (on a first grant, or when the client/request explicitly asks for
                // re-consent) Explicit clients don't get a silent prompt=none pass, and don't fall
                // back to Microsoft's consent UI (ConsentTypes.External) either -- this server owns
                // the consent decision for every client type it prompts at all.
                if (request.HasPromptValue(PromptValues.None) ||
                    string.Equals(consentType, ConsentTypes.External, StringComparison.Ordinal))
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Interactive user consent is required."
                        }));
                }

                var consentViewModel = await BuildConsentViewModelAsync(request, application, user!, HttpContext.RequestAborted);
                _logger.LogInformation(
                    "[oauth.consent.prompt] clientId={ClientId} sub={Sub} scopes={Scopes} redirectHost={Host}",
                    request.ClientId, await _userManager.GetUserIdAsync(user!), string.Join(" ", request.GetScopes()), consentViewModel.RedirectHost);

                return View("Consent", consentViewModel);
            }

            var identity = new ClaimsIdentity(
                        authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                        nameType: Claims.PreferredUsername,
                        roleType: Claims.Role);

            // Add the claims that will be persisted in the tokens.
            identity.SetClaim(Claims.Subject, await _userManager.GetUserIdAsync(user!))
                    .SetClaim(Claims.Email, await _userManager.GetEmailAsync(user!))
                    .SetClaim(Claims.Name, displayName ?? await _userManager.GetUserNameAsync(user!))
                    .SetClaim(Claims.PreferredUsername, await _userManager.GetUserNameAsync(user!))
                    .SetClaims(Claims.Role, [.. (await _userManager.GetRolesAsync(user!))]);

            // Note: in this sample, the granted scopes match the requested scope
            // but you may want to allow the user to uncheck specific scopes.
            // For that, simply restrict the list of scopes before calling SetScopes.
            identity.SetScopes(request.GetScopes());
            identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

            // Automatically create a permanent authorization to avoid requiring explicit consent
            // for future authorization or token requests containing the same scopes.
            var authorization = authorizations.LastOrDefault();
            authorization ??= await _authorizationManager.CreateAsync(
                identity: identity,
                subject: await _userManager.GetUserIdAsync(user!),
                client: await _applicationManager.GetIdAsync(application),
                type: AuthorizationTypes.Permanent,
                scopes: identity.GetScopes());

            identity.SetAuthorizationId(await _authorizationManager.GetIdAsync(authorization));
            identity.SetDestinations(GetDestinations);

            _logger.LogInformation(
                "[oauth.consent.grant] clientId={ClientId} sub={Sub} authorizationId={AuthorizationId}",
                request.ClientId, await _userManager.GetUserIdAsync(user!), identity.GetAuthorizationId());

            // TODO: If external cookie is removed the user needs to enter credentials again when page refresh. Review convenience.
            // await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            await IssueCookieSessionAsync(identity, "authorize");

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // The "Deny" half of the consent screen. Anonymous (not bare [Authorize]): a bare
        // [Authorize] resolves to the app's default policy, whose authenticate scheme is
        // OpenIddictValidationAspNetCoreDefaults (a JWT bearer scheme) -- a browser form POST from
        // Views/OAuth/Consent.cshtml never carries one, so that attribute made this action
        // permanently unreachable. Every other action in this controller is anonymous already;
        // [ValidateAntiForgeryToken] is what actually protects this one.
        [HttpPost("authorize")]
        [FormValueRequired("submit.Deny", excludedName: "submit.Accept")]
        [ValidateAntiForgeryToken]
        // Notify OpenIddict that the authorization grant has been denied by the resource owner
        // to redirect the user agent to the client application using the appropriate response_mode.
        public async Task<IActionResult> Deny()
        {
            // HttpContext.User is whatever the app's *default* authenticate scheme
            // (OpenIddictValidation, a JWT bearer scheme) produced -- always unauthenticated here,
            // since this is a browser form POST with no bearer token. The signed-in user's identity
            // for this consent decision lives on the external cookie instead, the same one
            // HandleAuthorizeAsync reads (IdentityConstants.ExternalScheme).
            var externalResult = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            var sub = externalResult.Succeeded
                ? externalResult.Principal?.FindFirst(ClaimTypes.Upn)?.Value
                    ?? externalResult.Principal?.FindFirst(ClaimTypes.Email)?.Value
                    ?? externalResult.Principal?.Identity?.Name
                : null;

            _logger.LogInformation(
                "[oauth.consent.deny] clientId={ClientId} sub={Sub}",
                HttpContext.GetOpenIddictServerRequest()?.ClientId, sub);

            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // Every requested scope the client holds no "scp:{scope}" permission for. Extracted as a
        // pure function (matching RequiresConsentPrompt below) so this closes cleanly against a
        // permission array without needing a live application/store -- see the caller's comment for
        // why this check exists.
        internal static string[] GetDeniedScopes(ImmutableArray<string> permissions, IEnumerable<string> requestedScopes)
            => requestedScopes.Where(scope => !permissions.Contains(Permissions.Prefixes.Scope + scope)).ToArray();

        // Implicit: never prompts (matches Authorize()'s historical unconditional auto-approve for
        // first-party clients). Systematic: always prompts, no matter how many prior authorizations
        // exist. Explicit (DCR-issued MCP clients) and any other value: prompts only when there is
        // no reusable permanent authorization yet, or the client/request explicitly asked for
        // re-consent via prompt=consent. consentGranted (the request came from Accept()) always
        // short-circuits to "no prompt needed" -- the user just granted it.
        internal static bool RequiresConsentPrompt(string? consentType, bool consentGranted, int existingAuthorizations, bool promptConsent)
        {
            if (consentGranted)
            {
                return false;
            }

            return consentType switch
            {
                var type when string.Equals(type, ConsentTypes.Implicit, StringComparison.Ordinal) => false,
                var type when string.Equals(type, ConsentTypes.Systematic, StringComparison.Ordinal) => true,
                _ => existingAuthorizations == 0 || promptConsent,
            };
        }

        private async Task<ConsentViewModel> BuildConsentViewModelAsync(
            OpenIddictRequest request, object application, OpenIdUser user, CancellationToken cancellationToken)
        {
            var redirectUri = new Uri(request.RedirectUri!);

            var scopes = new List<ConsentScopeViewModel>();
            foreach (var scope in request.GetScopes())
            {
                var scopeEntity = await _scopeManager.FindByNameAsync(scope, cancellationToken);
                var displayName = scopeEntity is not null ? await _scopeManager.GetDisplayNameAsync(scopeEntity, cancellationToken) : null;
                var description = scopeEntity is not null ? await _scopeManager.GetDescriptionAsync(scopeEntity, cancellationToken) : null;

                scopes.Add(new ConsentScopeViewModel
                {
                    Name = scope,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? FallbackScopeDisplayName(scope) : displayName,
                    Description = string.IsNullOrWhiteSpace(description) ? FallbackScopeDescription(scope) : description,
                });
            }

            // Re-emitted as hidden fields so the consent form's POST carries the entire original
            // OAuth request back to Accept()/Deny(). submit.* and the antiforgery field are excluded
            // so a re-render (e.g. after a validation error) never duplicates them.
            var source = Request.HasFormContentType
                ? (IEnumerable<KeyValuePair<string, StringValues>>)Request.Form
                : Request.Query;

            var parameters = new List<KeyValuePair<string, string>>();
            foreach (var pair in source)
            {
                if (pair.Key.StartsWith("submit.", StringComparison.Ordinal) ||
                    string.Equals(pair.Key, "__RequestVerificationToken", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var value in pair.Value)
                {
                    parameters.Add(new KeyValuePair<string, string>(pair.Key, value ?? string.Empty));
                }
            }

            return new ConsentViewModel
            {
                ApplicationName = await _applicationManager.GetDisplayNameAsync(application, cancellationToken) is { Length: > 0 } name ? name : request.ClientId!,
                ClientId = request.ClientId!,
                RedirectHost = redirectUri.Host,
                IsLoopbackRedirect = Program.IsLoopbackHost(redirectUri.Host),
                UserName = await _userManager.GetUserNameAsync(user) ?? string.Empty,
                Scopes = scopes,
                Parameters = parameters,
                QueryString = Request.QueryString.Value ?? string.Empty,
            };
        }

        // Fallback copy for the standard OIDC scopes, which have no row in the scope store (only
        // application-defined scopes like "mcp" or "api/.default" are seeded -- see
        // MigrationExtension.RegisterApplications). Falling back to the raw scope name on a
        // security-critical consent page reads badly, so these read as short user-facing sentences.
        // Routed through LocalizationService.GetString like every other user-facing string on this
        // page (Consent.cshtml uses @Localizer[...], which reads from the same IStringLocalizer<Locale>
        // this static helper wraps), per apps/api/CLAUDE.md's "use IStringLocalizer /
        // LocalizationService for user-facing strings, not literals."
        private static string FallbackScopeDisplayName(string scope) => scope switch
        {
            Scopes.OpenId => LocalizationService.GetString("Sign you in"),
            Scopes.OfflineAccess => LocalizationService.GetString("Stay signed in when you're not using the app"),
            Scopes.Email => LocalizationService.GetString("View your email address"),
            Scopes.Profile => LocalizationService.GetString("View your basic profile"),
            Scopes.Roles => LocalizationService.GetString("View your roles"),
            _ => scope,
        };

        private static string? FallbackScopeDescription(string scope) => scope switch
        {
            Scopes.OpenId => LocalizationService.GetString("Confirms who you are."),
            Scopes.OfflineAccess => LocalizationService.GetString("Lets this application refresh its access without you signing in again."),
            _ => null,
        };

        private static IEnumerable<string> GetDestinations(Claim claim)
        {
            // Note: by default, claims are NOT automatically included in the access and identity tokens.
            // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
            // whether they should be included in access tokens, in identity tokens or in both.            
            switch (claim.Type)
            {
                case Claims.Subject:
                case Claims.Name:
                case Claims.PreferredUsername:
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                    yield break;

                case Claims.Email:
                case Claims.Role:
                    yield return Destinations.IdentityToken;
                    yield break;

                // Never include the security stamp in the access and identity tokens, as it's a secret value.
                case "AspNet.Identity.SecurityStamp":
                    yield break;

                case "tid":
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                    yield break;

                default:
                    yield return Destinations.IdentityToken;
                    yield break;
            }
        }

        private long? ResolveDefaultTenat(string identityName, long? accountId)
        {
            if (accountId.HasValue && accountId.Value == 0)
                accountId = null;

            long? currentAccountId = null;
            // If AccountId provided, try to check if user exists in that tenant
            if (accountId.HasValue)
            {
                currentAccountId = _cacheService.GetOrAdd($"tenant:{accountId}:identity:{identityName}:exists", ctx =>
                {
                    ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                    var connectionString = _configuration.GetConnectionString("Database");
                    using var npgsqlConnection = new NpgsqlConnection(connectionString);
                    npgsqlConnection.Open();
                    using var cmd = npgsqlConnection.CreateCommand();
                    cmd.CommandText = "SELECT account_id FROM cs.identity WHERE name = @name and account_id = @account_id LIMIT 1";
                    cmd.Parameters.Add(new NpgsqlParameter("name", identityName.ToLowerInvariant()));
                    cmd.Parameters.Add(new NpgsqlParameter("account_id", accountId.Value));
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        return (long)reader["account_id"];
                    }
                    return (long?)null;
                });
            }

            if (currentAccountId.HasValue)
                // User exists in provided AccountId, hence return it
                return currentAccountId;

            if (accountId.HasValue)
            {
                // User not exists in provided AccountId, so fail
                return null;
            }

            // AccountId not provided, try to resolve the first accountId that identity belongs to
            currentAccountId = _cacheService.GetOrAdd($"identity:{identityName}:tenant:default", ctx =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
                var connectionString = _configuration.GetConnectionString("Database");
                using var npgsqlConnection = new NpgsqlConnection(connectionString);
                npgsqlConnection.Open();
                using var cmd = npgsqlConnection.CreateCommand();
                cmd.CommandText = "SELECT account_id FROM cs.identity WHERE name = @name LIMIT 1";
                cmd.Parameters.Add(new NpgsqlParameter("name", identityName.ToLowerInvariant()));
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return (long)reader["account_id"];
                }
                return (long?)null;
            });

            return currentAccountId;
        }
    }
}
