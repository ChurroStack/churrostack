using ChurrOS.Api.Domain.Auth;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.AspNet;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
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

        public OAuthController(
            IOpenIddictApplicationManager applicationManager,
            IOpenIddictAuthorizationManager authorizationManager,
            IOpenIddictScopeManager scopeManager,
            SignInManager<OpenIdUser> signInManager,
            UserManager<OpenIdUser> userManager,
            ICacheService cacheService,
            IConfiguration configuration)
        {
            _applicationManager = applicationManager;
            _authorizationManager = authorizationManager;
            _scopeManager = scopeManager;
            _signInManager = signInManager;
            _userManager = userManager;
            _cacheService = cacheService;
            _configuration = configuration;
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
        public async Task<IActionResult> Authorize()
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

                var prompt = string.Join(" ", request.GetPromptValues().Remove(PromptValues.Login));

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

            // Retrieve the permanent authorizations associated with the user and the calling client application.
            var authorizations = await _authorizationManager.FindAsync(
                subject: await _userManager.GetUserIdAsync(user!),
                client: await _applicationManager.GetIdAsync(application),
                status: Statuses.Valid,
                type: AuthorizationTypes.Permanent,
                scopes: request.GetScopes()).ToListAsync();

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

            // TODO: If external cookie is removed the user needs to enter credentials again when page refresh. Review convenience.
            // await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        [Authorize, FormValueRequired("submit.Deny")]
        [HttpPost("authorize"), ValidateAntiForgeryToken]
        // Notify OpenIddict that the authorization grant has been denied by the resource owner
        // to redirect the user agent to the client application using the appropriate response_mode.
        public IActionResult Deny() => Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

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
