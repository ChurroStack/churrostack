using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Domain.Auth;
using ChurrOS.Api.Jobs;
using ChurrOS.Api.Middlewares;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Redis;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Services.Share;
using ChurrOS.Api.Utils;
using ChurrOS.ServiceDefaults;
using DispatchR.Extensions;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;
using HeaderNames = Microsoft.Net.Http.Headers.HeaderNames;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Quartz;
using Quartz.AspNetCore;
using StackExchange.Redis;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
#if !DEBUG
using System.Security.Cryptography.X509Certificates;
#endif
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Yarp.ReverseProxy.Configuration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ChurrOS.Api
{
    public class Program
    {
        private record EnvInfo(string EncryptionKey, int Port, long AccountId);
        private record RoutingInfo(ApplicationMode Mode, string? DeploymentName);

        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSignalR().AddStackExchangeRedis(builder.Configuration.GetConnectionString("Cache")!);

            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 10_000_000_000;
                // Microsoft Entra ID auth codes (1–4 KB) plus the encrypted ASP.NET
                // state in /oauth/callback/microsoft can push the request line past
                // Kestrel's 8 KB default and surface as ERR_CONNECTION_CLOSED. Same
                // story for the accumulated correlation/nonce cookies on the same
                // request.
                serverOptions.Limits.MaxRequestLineSize = 32 * 1024;
                serverOptions.Limits.MaxRequestHeadersTotalSize = 64 * 1024;
            });

            builder.Services.Configure<FormOptions>(o =>
            {
                o.MultipartBodyLengthLimit = 10_000_000_000;
            });

            builder.Services
                // AddControllersWithViews (not AddControllers): the OAuth consent screen
                // (OAuthController.Authorize -> Views/OAuth/Consent.cshtml) is server-rendered HTML,
                // not JSON -- it renders two attacker-controlled strings (a DCR client's self-asserted
                // client_name and its redirect URI), so Razor's automatic HTML encoding is load-bearing.
                // This also registers antiforgery services, which nothing in this project configured
                // explicitly before now (see the explicit AddAntiforgery() call below).
                .AddControllersWithViews(options =>
                {
                    options.Filters.Add(new ResponseExceptionFilter());
                    options.InputFormatters.Add(new Utils.AspNet.TextInputFormatter());
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ApplyDefaultOptions();
                    JsonSettings.Value = options.JsonSerializerOptions;
                });

            // Explicit rather than relying on AddControllersWithViews' implicit registration:
            // OAuthController.Accept and LoginController.SignOut both carry
            // [ValidateAntiForgeryToken], and the consent view calls IAntiforgery directly to emit
            // the token, so this needs to be guaranteed present rather than assumed.
            builder.Services.AddAntiforgery();

            // /oauth/register (DCR) is anonymous, unauthenticated, and now accepts any https
            // redirect_uri (not just loopback -- see OAuthController.Register), so it's reachable by
            // anyone on the internet and creates a permanent OpenIddict application row per call.
            // A minimal per-IP throttle, not an attempt at a full abuse-prevention system.
            builder.Services.AddRateLimiter(options =>
            {
                // The middleware's own default (503) reads as "the server is down", not "you're
                // being throttled" -- 429 is what RFC 6585 defines for this and what an OAuth/HTTP
                // client actually expects to see and retry on.
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // AddFixedWindowLimiter (unpartitioned) would be one shared window for every
                // caller -- one client exhausting it blocks every other client's registration for
                // the rest of the window. AddPolicy + GetFixedWindowLimiter gives each remote IP
                // its own window instead. RemoteIpAddress is safe to key on here specifically
                // because app.UseForwardedHeaders() (below, with KnownProxies/KnownNetworks
                // cleared) already runs ahead of this and rewrites it from X-Forwarded-For, so this
                // is the real client IP behind nginx/YARP, not the proxy's.
                options.AddPolicy("dcr", httpContext => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            });

            // Add service defaults & Aspire client integrations.
            builder.AddServiceDefaults();

            // Add services to the container.
            builder.Services.AddProblemDetails();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "CurrOS", Version = "v1" });
                options.AddSecurityDefinition(
                "Bearer",
                    new OpenApiSecurityScheme
                    {
                        In = ParameterLocation.Header,
                        Description = "Please enter a valid token.",
                        Name = "Authorization",
                        Type = SecuritySchemeType.Http,
                        BearerFormat = "JWT",
                        Scheme = "Bearer",
                    }
                );
                options.AddSecurityRequirement(document => new() { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] });
            });
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddLazyCache();
            builder.Services.AddMapster();
            builder.Services.AddDispatchR(cfg => cfg.Assemblies.Add(typeof(Program).Assembly));
            // DispatchR registers handlers only via IRequestHandler<,> — see DispatchR 2.1.1.
            // Shared utilities consumed by multiple handlers must be registered explicitly here.
            builder.Services.AddScoped<ChurrOS.Api.Services.IMetricsBucketService, ChurrOS.Api.Services.MetricsBucketService>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Locales");
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
                // ASP.NET trusts only loopback by default; in container/k8s
                // deployments the upstream proxy may sit on a different
                // network. Clearing both lists honours X-Forwarded-* from any
                // upstream so the middleware reorder below is load-bearing.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetConnectionString("Cache");
                options.InstanceName = "churros_";
            });
            builder.Services.AddSingleton(TypeAdapterConfig.GlobalSettings);
            builder.Services.AddScoped<IMapper, ServiceMapper>();
            var redisConnectionString = builder.Configuration.GetConnectionString("Cache") ?? throw new InvalidOperationException("Connection string 'Redis' not found.");
            builder.Services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(redisConnectionString), "churros_protection_keys"); ;
            TypeAdapterConfig.GlobalSettings.Scan(typeof(ChurrosDbContext).Assembly);
            var databaseConnectionString = builder.Configuration.GetConnectionString("Database") ?? throw new InvalidOperationException("Connection string 'Database' not found.");
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(databaseConnectionString.ToString());
            dataSourceBuilder.EnableDynamicJson();
            var npgsqlDataSource = dataSourceBuilder.Build();
            builder.Services.AddDbContext<ChurrosDbContext>(options =>
            {
                options.UseNpgsql(npgsqlDataSource, o =>
                {
                    o.MigrationsAssembly(typeof(ChurrosDbContext).Assembly.FullName);
                    o.CommandTimeout((int)TimeSpan.FromMinutes(1).TotalSeconds);
                })
                .UseSnakeCaseNamingConvention();
#if DEBUG
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
#endif
                options.ConfigureWarnings(o => o.Ignore(RelationalEventId.PendingModelChangesWarning));
                options.UseOpenIddict<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, Guid>();
            }, ServiceLifetime.Transient);
            builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    name: "Default",
                    policy =>
                    {
                        var origins = builder.Configuration["Cors:Origins"]!
                            .Split(',', StringSplitOptions.RemoveEmptyEntries);
                        policy.WithOrigins(origins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            // Lets browser-driven MCP clients read the 401 challenge's
                            // WWW-Authenticate header cross-origin (not exposed by default).
                            .WithExposedHeaders(HeaderNames.WWWAuthenticate);
                        // Credentials (the .AspNetCore.Cookies session co-issued by
                        // /oauth/token) require an explicit origin allow-list — the
                        // CORS spec forbids combining wildcard with credentials and
                        // ASP.NET throws InvalidOperationException at startup if both
                        // are set. Dev / permissive deployments with "*" therefore
                        // skip AllowCredentials; production deployments configure
                        // specific origins and get the cookie session over CORS.
                        if (!origins.Contains("*"))
                        {
                            policy.AllowCredentials();
                        }
                    }
                );
            });

            // Register the Identity services.
            builder.Services
                .AddIdentity<OpenIdUser, OpenIdRole>(config =>
                {
                    config.User.AllowedUserNameCharacters = ":abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                    config.SignIn.RequireConfirmedEmail = false;
                    config.SignIn.RequireConfirmedPhoneNumber = false;
                    config.SignIn.RequireConfirmedAccount = false;

                    config.ClaimsIdentity.UserNameClaimType = Claims.PreferredUsername;
                    config.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                    config.ClaimsIdentity.RoleClaimType = Claims.Role;
                    config.ClaimsIdentity.EmailClaimType = Claims.Email;
                })
                .AddEntityFrameworkStores<ChurrosDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.Configure<IdentityOptions>(options =>
            {
                options.ClaimsIdentity.UserNameClaimType = Claims.PreferredUsername;
                options.ClaimsIdentity.RoleClaimType = "role";
            });

            // The canonical resource identifier for the MCP server, reusing the same BaseUrl config
            // key that already sets the OpenIddict issuer below. Computed once here (rather than
            // inside any of the AddOpenIddict()/AddAuthentication() configuration lambdas) so it can
            // be shared by the resource/scope registration, the MCP ResourceMetadata, and the
            // McpPolicy audience check without re-reading configuration in each place. Also used by
            // OAuthController.Register (its "rsrc:" permission) and MigrationExtension (seeding the
            // "mcp" scope's Resources) -- GetMcpResource is the single source of truth so those three
            // call sites can never drift apart on normalization.
            var mcpResource = GetMcpResource(builder.Configuration);

            // Shared with both the OpenIddict issuer (below) and the MCP ResourceMetadata's
            // AuthorizationServers entry: discovery always publishes Issuer.AbsoluteUri, which for a
            // host-only URI carries a trailing slash. Deriving both from the same Uri instance (rather
            // than one from Issuer.AbsoluteUri and the other from BaseUrl.TrimEnd('/')) guarantees a
            // byte-exact match, which RFC 8414 §3.3 issuer comparisons require.
            var issuerUri = new Uri(builder.Configuration["BaseUrl"]!);

            // Register the OpenIddict server components.
            builder.Services.AddOptions();
            builder.Services.AddOpenIddict()
                .AddCore(options =>
                {
                    options
                        .SetDefaultApplicationEntity<OpenIdApplication>()
                        .SetDefaultAuthorizationEntity<OpenIdAuthorization>()
                        .SetDefaultScopeEntity<OpenIdScope>()
                        .SetDefaultTokenEntity<OpenIdToken>();

                    // Configure OpenIddict to use the Entity Framework Core stores and models.
                    options.UseEntityFrameworkCore()
                        .UseDbContext<ChurrosDbContext>()
                        .ReplaceDefaultEntities<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, Guid>();

                    // Enable Quartz.NET integration.
                    options.UseQuartz();
                })
                .AddServer(options =>
                {
                    options.DisableAccessTokenEncryption();

#if DEBUG
                    options.AddDevelopmentEncryptionCertificate()
                        .AddDevelopmentSigningCertificate();
#else
                    if (File.Exists("/app/certs/signing.pfx") && File.Exists("/app/certs/encryption.pfx"))
                    {
                        var signingCert = X509CertificateLoader.LoadPkcs12FromFile("/app/certs/signing.pfx", builder.Configuration["OPENIDDICT_SIGNING_PASSWORD"]);
                        var encryptionCert = X509CertificateLoader.LoadPkcs12FromFile("/app/certs/encryption.pfx", builder.Configuration["OPENIDDICT_ENCRYPTION_PASSWORD"]);

                        options.AddSigningCertificate(signingCert);
                        options.AddEncryptionCertificate(encryptionCert);
                    }
                    else
                    {
                        Console.WriteLine("ERROR: Certificate /app/certs/signing.pfx not found!");
                        throw new Exception("ERROR: Certificate /app/certs/signing.pfx not found!");
                    }
#endif

                    options.Configure(config => { config.Issuer = issuerUri; });

                    // Enable the token endpoint.
                    options
                        .SetTokenEndpointUris("oauth/token")
                        .SetAuthorizationEndpointUris("oauth/authorize")
                        .SetEndSessionEndpointUris("oauth/logout")
                        .SetUserInfoEndpointUris("oauth/userinfo");

                    //// Register protocol supported scopes.
                    //options.RegisterScopes(OpenIddict.Abstractions.OpenIddictConstants.Scopes.OpenId,
                    //    OpenIddict.Abstractions.OpenIddictConstants.Scopes.Email,
                    //    OpenIddict.Abstractions.OpenIddictConstants.Scopes.Profile,
                    //    OpenIddict.Abstractions.OpenIddictConstants.Scopes.Roles,
                    //    OpenIddict.Abstractions.OpenIddictConstants.Scopes.OfflineAccess);

                    // Enable authorization code flow
                    options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();

                    // Enable refresh token code flow
                    options.AllowRefreshTokenFlow();

                    // Enable the client credentials flow.
                    options.AllowClientCredentialsFlow();

                    // Enable the token exchange flow.
                    options.AllowTokenExchangeFlow();

                    //// Token protection
                    //options.UseDataProtection()
                    //    .PreferDefaultAccessTokenFormat()
                    //    .PreferDefaultAuthorizationCodeFormat()
                    //    .PreferDefaultDeviceCodeFormat()
                    //    .PreferDefaultRefreshTokenFormat()
                    //    .PreferDefaultUserCodeFormat();

                    //options.UseReferenceAccessTokens();

                    // Register the ASP.NET Core host and configure the ASP.NET Core options.
                    options.UseAspNetCore()
                        .EnableAuthorizationEndpointPassthrough()
                        .EnableEndSessionEndpointPassthrough()
                        .EnableTokenEndpointPassthrough()
                        //.EnableUserinfoEndpointPassthrough()
                        .EnableStatusCodePagesIntegration()
                        .DisableTransportSecurityRequirement();

                    options.RemoveEventHandler(OpenIddictServerHandlers.Authentication.ValidateClientRedirectUri.Descriptor);
                    options.AddEventHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(builder =>
                    {
                        builder.UseInlineHandler(async context =>
                        {
                            if (context is null)
                            {
                                throw new ArgumentNullException(nameof(context));
                            }

                            if (string.IsNullOrEmpty(context.RedirectUri))
                            {
                                if (context.Request.HasScope(Scopes.OpenId))
                                {
                                    context.Reject(error: Errors.InvalidRequest, description: "Invalid redirect_uri parameter.", uri: null);
                                }

                                return;
                            }

                            if (!Uri.TryCreate(context.RedirectUri, UriKind.Absolute, out var redirectUri))
                            {
                                context.Reject(error: Errors.InvalidRequest, description: "Invalid redirect_uri parameter.", uri: null);
                                return;
                            }

                            var httpRequest = context.Transaction.GetHttpRequest()!;

                            // Three client-type-specific rules, not a blanket exact/none check. Neither
                            // "api" nor "app" compares against httpRequest.Scheme: the defence-in-depth
                            // middleware later in this file unconditionally forces Request.Scheme to
                            // "https" before OpenIddict's pipeline runs, so by the time this handler
                            // executes httpRequest.Scheme is always "https" -- comparing against it would
                            // reject genuine http loopback traffic (local dev, native/CLI clients) rather
                            // than validate anything. IsSameOriginRedirectUri checks the *redirect_uri's*
                            // own scheme instead (https required, except for a loopback host).
                            //  - "api" (confidential): only ever used for its fixed self-loopback OIDC
                            //    callback (Program.cs AddOpenIdConnect CallbackPath="/login/signin-oidc").
                            //    Matched by same-origin + exact path rather than a BaseUrl-derived literal,
                            //    because the OIDC handler builds its redirect_uri from the *live* request
                            //    host (ASP.NET's OpenIdConnectHandler default), which only equals BaseUrl
                            //    when the app happens to be reached through that exact host.
                            //  - "app" (PWA, public): oidc-spa derives its redirect from
                            //    window.location.origin + the app's Vite BASE_URL ("/", per
                            //    vite.config.ts), never from the current page path -- verified directly
                            //    against oidc-spa's homeAndRedirectUri.js source. So the PWA's redirect_uri
                            //    is always exactly "<origin>/", one fixed path, not an open set: matched by
                            //    same-origin + exact path "/". Requiring the path (not just the origin)
                            //    closes off tenant-controlled same-origin paths like /share/{app}/{port} as
                            //    redirect targets.
                            //  - everything else: a DCR-registered public client (see OAuthController.Register).
                            //    Native/CLI clients can't predict their ephemeral local port (RFC 8252 §7.3),
                            //    so a *registered* loopback host matches any incoming port; the exemption is
                            //    gated on the registered host, not the incoming one.
                            var isValid = context.ClientId switch
                            {
                                "api" => IsApiRedirectUri(redirectUri, httpRequest.Host.Host),

                                "app" => IsAppRedirectUri(redirectUri, httpRequest.Host.Host),

                                _ => await IsRegisteredLoopbackAwareRedirectUriAsync(
                                    httpRequest.HttpContext.RequestServices.GetRequiredService<IOpenIddictApplicationManager>(),
                                    context.ClientId!,
                                    redirectUri),
                            };

                            if (!isValid)
                            {
                                context.Reject(error: Errors.InvalidRequest, description: "Invalid redirect_uri parameter.", uri: null);
                            }
                        });

                        builder.SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientRedirectUri.Descriptor.Order);
                    });


                    options.IgnoreScopePermissions();

                    // MCP server resource/scope: OpenIddict validates the RFC 8707 `resource`
                    // request parameter by default (DisableResourceValidation defaults to false),
                    // so registering it here is required -- without it every MCP client's
                    // authorize/token request fails invalid_target before a browser even opens.
                    // RegisterResources/RegisterScopes only configure server *options* (what
                    // discovery advertises and what a `resource`/`scope` parameter is allowed to
                    // request) -- they do NOT create a scope row in the store. The actual
                    // aud=<mcpResource> stamping in OAuthController (via
                    // identity.SetResources(await _scopeManager.ListResourcesAsync(...))) reads
                    // ListResourcesAsync, which is purely store-backed and knows nothing about these
                    // options. The "mcp" scope's row (with mcpResource in its Resources) is seeded in
                    // MigrationExtension.RegisterApplications, the same place the "api"/"app"
                    // applications and the "api/.default" scope are seeded -- without that row, every
                    // MCP token request/response omits aud=<mcpResource> and McpPolicy always 403s.
                    options.RegisterResources(mcpResource.AbsoluteUri);
                    options.RegisterScopes("mcp");

                    // Advertise the DCR endpoint (OAuthController.Register) in discovery.
                    // OpenIddict has no built-in RFC 7591 support (tracked upstream in
                    // openiddict-core#2404, unimplemented even in the 8.0 previews), so this is
                    // metadata injection only -- the endpoint itself is a plain controller action.
                    options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handlerBuilder =>
                    {
                        handlerBuilder.UseInlineHandler(context =>
                        {
                            context.Metadata["registration_endpoint"] = new Uri(mcpResource, "/oauth/register").AbsoluteUri;
                            return default;
                        });
                    });

                    // Register custom scope validator
                    options.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(builder =>
                    {
                        builder.UseInlineHandler(async context =>
                        {
                            var appManager = context.Transaction.GetHttpRequest()?.HttpContext?.RequestServices.GetRequiredService<IOpenIddictApplicationManager>()!;
                            var application = (OpenIdApplication?)await appManager.FindByClientIdAsync(context.ClientId!);
                            var requestedScopes = context.Request?.GetScopes();
                            if (requestedScopes is not null && requestedScopes.Value.Length > 0)
                            {
                                var permissions = JsonSerializer.Deserialize<string[]>(application!.Permissions!)!.ToHashSet();
                                foreach (var requestedScope in requestedScopes)
                                {
                                    if (!permissions.Contains($"scp:{requestedScope}"))
                                    {
                                        if (context.ClientId == "api")
                                        {
                                            var appId = requestedScope.Split('/').First();
                                            var app = await appManager.FindByClientIdAsync(appId);
                                            if (app is null)
                                                context.Reject("This client application is not allowed to use the specified scope.", "https://documentation.openiddict.com/errors/ID2051");
                                        }
                                        else
                                        {
                                            context.Reject("This client application is not allowed to use the specified scope.", "https://documentation.openiddict.com/errors/ID2051");
                                        }
                                    }
                                }
                            }
                        });
                    });

                    options.RemoveEventHandler(OpenIddictServerHandlers.Session.ValidateClientPostLogoutRedirectUri.Descriptor);
                    options.AddEventHandler<OpenIddictServerEvents.ValidateEndSessionRequestContext>(builder =>
                    {
                        builder.UseInlineHandler(async context =>
                        {
                            if (context is null)
                            {
                                throw new ArgumentNullException(nameof(context));
                            }

                            if (string.IsNullOrEmpty(context.PostLogoutRedirectUri))
                            {
                                context.Reject(error: Errors.InvalidRequest, description: "Invalid post_logout_redirect_uri parameter.", uri: null);
                            }

                            // If an optional post_logout_redirect_uri was provided, validate it.
                            if (!Uri.TryCreate(context.PostLogoutRedirectUri, UriKind.Absolute, out Uri? uri))
                            {
                                context.Reject(error: Errors.InvalidRequest, description: "Invalid post_logout_redirect_uri parameter.", uri: null);
                            }

                            if (!string.IsNullOrEmpty(uri?.Fragment))
                            {
                                context.Reject(error: Errors.InvalidRequest, description: "Invalid post_logout_redirect_uri parameter.", uri: null);
                            }
                        });

                        builder.SetOrder(OpenIddictServerHandlers.Session.ValidateClientPostLogoutRedirectUri.Descriptor.Order);
                    });
                })
                .AddValidation(options =>
                {
                    // Import the configuration from the local OpenIddict server instance.
                    options.UseLocalServer();

                    //// Token protection
                    // options.UseDataProtection();

                    //// Enforce token entry validation for each API request
                    // options.EnableTokenEntryValidation();

                    // Register the ASP.NET Core host.
                    options.UseAspNetCore();

                    //options.Configure(config =>
                    //{
                    //    config.TokenValidationParameters.NameClaimType = Claims.Subject;
                    //    config.TokenValidationParameters.RoleClaimType = Claims.Role;
                    //});

                    options.Configure(config =>
                    {
                        config.TokenValidationParameters.AuthenticationType = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                        config.TokenValidationParameters.NameClaimType = Claims.PreferredUsername;
                        config.TokenValidationParameters.RoleClaimType = Claims.Role;
                    });
                });

            var authBuilder = builder.Services
                .AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                    o.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/login/signin";
                    options.LogoutPath = "/login/signout";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.Cookie.SameSite = SameSiteMode.None;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                })
                .AddOpenIdConnect(options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.CorrelationCookie.SameSite = SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.NonceCookie.SameSite = SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.CallbackPath = "/login/signin-oidc";
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.ResponseMode = OpenIdConnectResponseMode.Query;

                    options.Authority = builder.Configuration["BaseUrl"];
                    options.ClientId = "api";
                    options.ClientSecret = builder.Configuration["ClientSecret"];
                    options.ResponseType = "code";

                    options.SaveTokens = true;
                    options.MapInboundClaims = false;

                    options.Scope.Clear();
                    options.Scope.Add("openid api/.default offline_access");

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = builder.Configuration["BaseUrl"],
                        ValidAudience = "api",
                        NameClaimType = Claims.PreferredUsername,
                        RoleClaimType = "role"
                    };
                });

            if (!string.IsNullOrWhiteSpace(builder.Configuration["ExternalProviders:Microsoft:ClientId"]))
            {
                authBuilder.AddMicrosoftAccount("external.microsoft", "Microsoft Account", microsoftOptions =>
                {
                    microsoftOptions.SignInScheme = IdentityConstants.ExternalScheme;
                    microsoftOptions.ClientId = builder.Configuration["ExternalProviders:Microsoft:ClientId"]!;
                    microsoftOptions.ClientSecret = builder.Configuration["ExternalProviders:Microsoft:ClientSecret"]!;
                    microsoftOptions.CallbackPath = "/oauth/callback/microsoft";
                });
            }

            authBuilder.AddMcp(options =>
            {
                // The SDK default (ForwardAuthenticate = "Bearer") targets a bare JwtBearer scheme
                // name that doesn't exist in this app -- tokens are validated by OpenIddict's own
                // validation scheme, so authentication would silently no-op (blanket 401) without
                // this override.
                options.ForwardAuthenticate = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.ResourceMetadata = new()
                {
                    Resource = mcpResource.AbsoluteUri,
                    // issuerUri.AbsoluteUri, not BaseUrl.TrimEnd('/'): discovery publishes
                    // config.Issuer.AbsoluteUri (set from this same issuerUri above), which for a
                    // host-only URI always carries a trailing slash. A client doing a byte-exact
                    // RFC 8414 §3.3 issuer comparison would otherwise abort discovery.
                    AuthorizationServers = { issuerUri.AbsoluteUri },
                    ScopesSupported = ["mcp"],
                };
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("JwtOrApiKeyPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes([ApiKeyAuthenticationHandler.SchemeName, OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme]);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("ApiKeyPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes(ApiKeyAuthenticationHandler.SchemeName);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("JwtPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("CookiePolicy", policy =>
                {
                    policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                });

                options.AddPolicy("AppJwtPolicy", policy =>
                {
                    policy
                        .AddAuthenticationSchemes([ApiKeyAuthenticationHandler.SchemeName, OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme])
                        .RequireAuthenticatedUser()
                        .AddRequirements(new ApplicationMemberAccessRequirement());
                });

                options.AddPolicy("AppCookiePolicy", policy =>
                {
                    policy
                        .AddAuthenticationSchemes([ApiKeyAuthenticationHandler.SchemeName, CookieAuthenticationDefaults.AuthenticationScheme])
                        .RequireAuthenticatedUser()
                        .AddRequirements(new ApplicationMemberAccessRequirement());
                });

                options.AddPolicy("McpPolicy", policy =>
                {
                    // McpAuthenticationHandler.HandleAuthenticateAsync always returns NoResult();
                    // its ForwardAuthenticate (set above, in AddMcp) transparently delegates the
                    // actual authentication work to OpenIddict's validation scheme, while its
                    // *challenge* is handled directly by McpAuthenticationHandler, which emits the
                    // WWW-Authenticate: Bearer resource_metadata="..." header MCP clients require.
                    // A dedicated policy (rather than flipping the app's global default schemes) is
                    // what lets /mcp opt into that behavior without changing every other endpoint's
                    // existing 401/challenge behavior.
                    policy.AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();

                    // Audience and scope both live under OpenIddict's private "oi_" claim
                    // prefix on the validated principal, not the public "aud"/"scope" claim
                    // types -- confirmed against OpenIddictValidationHandlers.Protection.cs,
                    // which falls back from "oi_aud" to the standard "aud" claim only if "oi_aud"
                    // is absent, and otherwise always re-derives "oi_aud" from it up front.
                    policy.RequireClaim(Claims.Private.Scope, "mcp");
                    policy.RequireClaim(Claims.Private.Audience, mcpResource.AbsoluteUri);
                });
            });

            builder.Services.AddQuartz(q =>
            {
                q.UseDefaultThreadPool(x => x.MaxConcurrency = 64);
                q.UsePersistentStore(options =>
                {
                    options.UsePostgres(sqlServer =>
                    {
                        sqlServer.ConnectionString = databaseConnectionString;
                    });

                    options.UseClustering();
                    options.UseProperties = true;
                    options.UseSystemTextJsonSerializer();
                });

                // Nightly Application Size recommendation analysis. Fires at 02:00 UTC
                // every day for every tenant; the timezone is pinned to UTC so the
                // schedule is independent of the container's local time.
                var analyzeUsageJobKey = new JobKey("analyze-usage-job");
                q.AddJob<AnalyzeUsageJob>(opts => opts.WithIdentity(analyzeUsageJobKey).StoreDurably());
                q.AddTrigger(opts => opts
                    .ForJob(analyzeUsageJobKey)
                    .WithIdentity("analyze-usage-trigger")
                    .WithCronSchedule("0 0 2 * * ?", x => x.InTimeZone(TimeZoneInfo.Utc)));

                var autoStopJobKey = new JobKey("auto-stop-evaluator-job");
                q.AddJob<AutoStopEvaluatorJob>(opts => opts.WithIdentity(autoStopJobKey).StoreDurably());
                q.AddTrigger(opts => opts
                    .ForJob(autoStopJobKey)
                    .WithIdentity("auto-stop-evaluator-trigger")
                    .WithCronSchedule("0 0/5 * * * ?", x => x.InTimeZone(TimeZoneInfo.Utc)));
            });

            builder.Services.AddQuartzServer(options =>
            {
                // when shutting down we want jobs to complete gracefully
                options.WaitForJobsToComplete = true;
            });

            builder.Services.Configure<QuartzOptions>(options =>
            {
                options.Scheduling.IgnoreDuplicates = true; // default: false
                options.Scheduling.OverWriteExistingData = true; // default: true
            });

            builder.Services.AddScoped<ITenantResolver, WebTenantResolver>();
            builder.Services.AddScoped<IAccountMembershipResolver, AccountMembershipResolver>();
            builder.Services.AddScoped<QuotaService, QuotaService>();
            builder.Services.AddSingleton<IIdGeneratorService, IdGenerationService>();
            builder.Services.AddSingleton<ICacheService, RedisCacheService>();
            builder.Services.AddSingleton<ILockService, RedisLockService>();
            builder.Services.AddSingleton<IQueueService, RedisStreamService>();
            builder.Services.AddSingleton<TemplateService, TemplateService>();
            builder.Services.AddSingleton<RunnerService, RunnerService>();
            builder.Services.AddSingleton<ClientNotificationService, ClientNotificationService>();
            builder.Services.AddSingleton<ProxyConfigurationProvider>();
            builder.Services.AddSingleton<IAuthorizationHandler, ApplicationMemberAccessHandler>();
            builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<ProxyConfigurationProvider>());
            builder.Services.AddSingleton<MetricsAggregatorService, MetricsAggregatorService>();
            builder.Services.AddSingleton<ChurrOS.Api.Services.AutoStart.AutoStartCache>();
            builder.Services.AddSingleton<ChurrOS.Api.Services.AutoStart.AutoStartCoordinator>();
            builder.Services.AddSingleton<ChurrOS.Api.Services.AutoStart.AutoStartTransform>();
            builder.Services.AddHostedService<TracesProcessorJob>();
            builder.Services.AddReverseProxy();
            // Interface that collects general metrics about the proxy forwarder
            builder.Services.AddMetricsConsumer<ForwarderMetricsConsumer>();
            // Registration of a consumer to events for proxy forwarder telemetry
            builder.Services.AddTelemetryConsumer<ForwarderTelemetryConsumer>();
            // Registration of a consumer to events for HttpClient telemetry
            builder.Services.AddTelemetryConsumer<HttpClientTelemetryConsumer>();
            builder.Services.AddTelemetryConsumer<WebSocketsTelemetryConsumer>();

            builder.Services.AddMcpServer()
                .WithTools<ChurrOS.Api.Mcp.ChurrosMcpTools>()
                .WithHttpTransport(options =>
                {
                    // Recommended for servers that don't need 2025-11-25 protocol revision
                    // server-to-client requests like sampling or elicitation (we don't): enables
                    // horizontal scaling without session affinity.
                    options.SessionMode = HttpServerSessionMode.Stateless;
                });

            var app = builder.Build();

            app.MapDefaultEndpoints();

            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapOpenApi();

            // ForwardedHeaders MUST run before HttpsRedirection so the
            // middleware can read X-Forwarded-Proto from the upstream proxy
            // (nginx proxies HTTP to Kestrel) and flip Request.Scheme to
            // "https". Without this order, HttpsRedirection sees raw scheme
            // "http" on every proxied request and issues a 307 — surfacing as
            // ERR_TOO_MANY_REDIRECTS or ERR_CONNECTION_CLOSED depending on the
            // CDN/WAF in front.
            app.UseForwardedHeaders();
#if !DEBUG
            app.UseHttpsRedirection();
#endif
            // Defence-in-depth: if X-Forwarded-Proto is ever stripped upstream
            // or UseForwardedHeaders is bypassed by a topology change, force
            // the scheme to https so URL generation (OIDC redirect URIs,
            // links, Set-Cookie Secure decisions) doesn't leak the
            // proxy-internal http:// scheme to clients.
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
            });

            // /share traffic is reverse-proxied through one or more YARP hops. With a streamed
            // request body, YARP can begin forwarding before the inbound body is fully received and
            // send 0 bytes while promising the Content-Length ("Sent 0 request content bytes, but
            // Content-Length promised N"); the downstream hop then waits for a body that never
            // arrives, the request is canceled, and it surfaces as an opaque 400. Buffering the body
            // so it is fully received and re-readable before forwarding fixes POST/PUT/PATCH through
            // the proxy. Only small bodies are buffered so large uploads keep streaming.
            const long shareRequestBufferLimit = 100 * 1024 * 1024;
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/share")
                    && context.Request.ContentLength is > 0 and <= shareRequestBufferLimit)
                {
                    context.Request.EnableBuffering();
                    await context.Request.Body.CopyToAsync(System.IO.Stream.Null, context.RequestAborted);
                    context.Request.Body.Position = 0;
                }
                await next();
            });

            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture("en"),
                SupportedCultures = [new CultureInfo("en"), new CultureInfo("es"), new CultureInfo("fr"), new CultureInfo("it"), new CultureInfo("pt")],
                SupportedUICultures = [new CultureInfo("en"), new CultureInfo("es"), new CultureInfo("fr"), new CultureInfo("it"), new CultureInfo("pt")]
            });

            app.UseCors("Default");
            app.UseWebSockets();
            app.UseAuthentication();
            app.UseMiddleware<MultiTenantMiddleware>();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapHub<NotificationHub>("/api/notifications");
            app.MapControllers();
            app.MapMcp("/mcp").RequireAuthorization("McpPolicy");

            var localizer = app.Services.GetRequiredService<IStringLocalizer<Locale>>();
            LocalizationService.Initialize(localizer);

            app.UsePerRequestMetricCollection();
            app.UseWebSocketsTelemetry();
            app.MapReverseProxy(pipeline =>
            {
                pipeline.Use(async (context, next) =>
                {
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        if (!string.IsNullOrEmpty(context.User.Identity.Name))
                        {
                            context.Request.Headers["X-User-Id"] = context.User.Identity.Name;
                        }
                    }

                    var proxyFeature = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>();
                    string? clusterId = proxyFeature?.Route.Cluster?.ClusterId;

                    if (string.IsNullOrEmpty(clusterId))
                    {
                        await next();
                        return;
                    }

                    var cacheService = context.RequestServices.GetService<ICacheService>()!;
                    var envInfo = await cacheService.GetOrAddAsync($"env:{clusterId}:info", async (ctx) =>
                    {
                        ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                        using var conn = new NpgsqlConnection(databaseConnectionString);
                        await conn.OpenAsync();
                        try
                        {
                            using var appCmd = new NpgsqlCommand($"""
                            SELECT
                                e.encryption_key,
                                e.port,
                                e.account_id,
                                a.encryption_key as account_encryption_key
                            FROM cs.environment e
                            JOIN cs.account a
                                ON a.id = e.account_id
                            WHERE e.name = '{clusterId}';
                            """, conn);
                            using var envReader = await appCmd.ExecuteReaderAsync();
                            if (envReader.Read())
                            {
                                var accountEncryptionKey = envReader["account_encryption_key"] as string;
                                var parts = accountEncryptionKey!.Split(':');
                                var masterKey = builder.Configuration["MasterKey"]!;
                                accountEncryptionKey = AesGcmEncryption.Decrypt(parts[0], masterKey, parts[1]);
                                var encryptedEncryptionKey = envReader["encryption_key"] as string;
                                parts = encryptedEncryptionKey!.Split(':');
                                var encryptionKey = AesGcmEncryption.Decrypt(parts[0], accountEncryptionKey, parts[1]);
                                return new EnvInfo(EncryptionKey: encryptionKey, Port: (int)envReader["port"], AccountId: (long)envReader["account_id"]);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
                        finally
                        {
                            await conn.CloseAsync();
                        }
                        return default;
                    }, context.RequestAborted);

                    var tenantResolver = context.RequestServices.GetService<ITenantResolver>()!;
                    tenantResolver.SetAccountId(envInfo!.AccountId);
                    if (!string.IsNullOrWhiteSpace(context.User?.Identity?.Name))
                    {
                        tenantResolver.SetIdentity(context.User.Identity.Name);
                    }
                    var quotaService = context.RequestServices.GetService<QuotaService>()!;
                    try
                    {
                        await quotaService.EnsureHasQuotaAsync(QuotaService.QuotaType.Network);
                    }
                    catch (Exception ex)
                    {
                        context.Response.StatusCode = 429;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = ex.Message
                        });
                        return;
                    }

                    if (context.Request.Path.StartsWithSegments($"/share"))
                    {
                        var parts = context.Request.Path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
                        var appName = parts[1];

                        var autoStartTransform = context.RequestServices.GetRequiredService<ChurrOS.Api.Services.AutoStart.AutoStartTransform>();
                        var shouldForward = await autoStartTransform.HandleShareRequestAsync(context, appName, envInfo.AccountId);
                        if (!shouldForward)
                        {
                            return;
                        }

                        var (appMode, destination) = await cacheService.GetOrAddAsync<RoutingInfo>($"app:{appName}:user:{context.User?.Identity?.Name}:routing_info", async (ctx) =>
                        {
                            ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                            var dbContext = context.RequestServices.GetService<ChurrosDbContext>()!;
                            var app = await dbContext.Set<Application>()
                                .Where(o => o.Name == appName && o.Mode == ApplicationMode.Workspace)
                                .Select(o => new { o.Id })
                                .SingleOrDefaultAsync();

                            if (app != null)
                            {
                                var deployment = await dbContext.Set<ApplicationDeployment>()
                                    .Where(o => o.ApplicationId == app.Id && o.OwnerId == dbContext.IdentityId)
                                    .SingleOrDefaultAsync();

                                return new RoutingInfo(ApplicationMode.Workspace, deployment?.Name);
                            }

                            return new RoutingInfo(ApplicationMode.Application, null);
                        }, context.RequestAborted);

                        if (appMode == ApplicationMode.Workspace)
                        {
                            if (string.IsNullOrEmpty(destination))
                            {
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsJsonAsync(new
                                {
                                    error = "Application deployment not found for the current user."
                                });
                                return;
                            }
                            context.Request.Headers.TryAdd("X-Destination-Id", destination);
                        }
                    }

                    using var hmac = new HMACSHA256(Convert.FromBase64String(envInfo.EncryptionKey));
                    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{context.Request.BuildCanonicalUrl()}:{clusterId}:{timestamp}"))).ToLower();
                    context.Request.Headers.TryAdd("X-Environment-Name", clusterId);
                    context.Request.Headers.TryAdd("X-Timestamp", timestamp.ToString());
                    context.Request.Headers.TryAdd("X-Signature", signature);
                    context.Request.Headers.TryAdd("X-Port", envInfo.Port.ToString());

                    await next();
                });
            });

            // Database migration & seeding
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();

            // Some data migrations (e.g. RetypeCpuGpuMetricsAsGauge) bulk-delete from
            // cs.metric_value, a Timescale hypertable that can grow large. Allow up
            // to 1h for the boot-time migration phase; runtime contexts keep the
            // default timeout because they get fresh DbContext instances from DI.
            var previousCommandTimeout = context.Database.GetCommandTimeout();
            context.Database.SetCommandTimeout(TimeSpan.FromHours(1));
            try
            {
                var pending = context.Database.GetPendingMigrations().ToArray();
                app.Logger.LogInformation(
                    "[boot] Starting EF Core migration (pending={PendingCount}, commandTimeout={CommandTimeoutSeconds}s){PendingList}",
                    pending.Length,
                    (int)TimeSpan.FromHours(1).TotalSeconds,
                    pending.Length > 0 ? $": {string.Join(", ", pending)}" : "");

                var migrateSw = System.Diagnostics.Stopwatch.StartNew();
                context.Database.Migrate();
                migrateSw.Stop();
                app.Logger.LogInformation(
                    "[boot] EF Core migration completed in {ElapsedMs} ms",
                    migrateSw.ElapsedMilliseconds);

                app.Logger.LogInformation("[boot] Applying TimescaleDB hypertables");
                var hyperSw = System.Diagnostics.Stopwatch.StartNew();
                await context.ApplyHypertablesAsync();
                hyperSw.Stop();
                app.Logger.LogInformation(
                    "[boot] TimescaleDB hypertables applied in {ElapsedMs} ms",
                    hyperSw.ElapsedMilliseconds);

                app.Logger.LogInformation("[boot] Applying Quartz schema");
                var quartzSw = System.Diagnostics.Stopwatch.StartNew();
                await context.ApplyQuartzTables();
                quartzSw.Stop();
                app.Logger.LogInformation(
                    "[boot] Quartz schema applied in {ElapsedMs} ms",
                    quartzSw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "[boot] Database migration phase failed");
                throw;
            }
            finally
            {
                context.Database.SetCommandTimeout(previousCommandTimeout);
            }

            var accountsRepo = context.Set<Account>();
            if (!accountsRepo.Any() && bool.TryParse(app.Configuration["CreateAccount"], out var createAccount) && createAccount)
            {
                var owners = app.Configuration["Owners"]?.Split(',', ';')?.Select(o => o.ToLowerInvariant()).ToArray();
                var domain = "localhost";

                await MigrationExtension.CreateAccountAsync(app.Configuration,
                    scope.ServiceProvider.GetRequiredService<ISchedulerFactory>(),
                    scope.ServiceProvider.GetRequiredService<IIdGeneratorService>(),
                    scope.ServiceProvider.GetRequiredService<TemplateService>(),
                    context, domain, owners
                );
            }

            await MigrationExtension.RegisterApplications(scope.ServiceProvider);
            await MigrationExtension.InitilizeTunnelUser(app.Configuration, context);

            var proxyConfig = scope.ServiceProvider.GetRequiredService<ProxyConfigurationProvider>();
            await proxyConfig.Initialize();

            app.Run();
        }

        // OAuth 2.1 / RFC 8252 §7.3: native/CLI public clients (DCR-registered MCP clients) can't
        // predict which ephemeral local port they'll bind, so a *registered* loopback redirect_uri
        // matches any incoming port on the same host+scheme+path. Non-loopback registered URIs
        // still require an exact match including port. The exemption is gated on the registered
        // host being loopback, not the incoming one, so a client can't claim it by registering a
        // non-loopback host.
        // internal (not private): OAuthController.Register also reads this, so DCR-issued
        // clients and this validator can never drift apart on what counts as loopback.
        internal static readonly string[] LoopbackHosts = ["127.0.0.1", "::1", "localhost"];

        // Uri.Host renders an IPv6 literal in bracket notation ("[::1]"), which never matches the
        // bracket-free "::1" entry in LoopbackHosts above -- so every direct LoopbackHosts.Contains
        // call site (this file's IsRegisteredLoopbackAwareRedirectUriAsync/IsSameOriginRedirectUri,
        // and OAuthController.Register) goes through this instead, which strips the brackets first.
        internal static bool IsLoopbackHost(string host)
            => LoopbackHosts.Contains(host.Trim('[', ']'), StringComparer.OrdinalIgnoreCase);

        // Computes the canonical MCP resource identifier from configuration. A single source of
        // truth for the three call sites that all need the exact same value: Main (resource/scope
        // registration, ResourceMetadata, McpPolicy's audience check), OAuthController.Register (the
        // "rsrc:" permission on DCR-issued clients), and MigrationExtension (seeding the "mcp"
        // scope's Resources) -- three independent `new Uri(new Uri(BaseUrl), "mcp")` expressions
        // with differing normalization was a drift bug waiting to happen, and McpPolicy compares the
        // audience by exact string.
        internal static Uri GetMcpResource(IConfiguration configuration)
            => new(new Uri(configuration["BaseUrl"]!), "mcp");

        // The "api" and "app" clients have no fixed registered redirect_uri to compare against (see
        // the rationale at the AddServer(...) call site in Main), so both are validated against the
        // current request's own origin instead, plus a fixed path checked separately by each caller.
        // Does NOT compare scheme against the *request's* scheme: the defence-in-depth middleware in
        // Main unconditionally forces Request.Scheme to "https" before OpenIddict's pipeline runs, so
        // the request's scheme is never a meaningful signal here. Instead this requires the
        // redirect_uri itself to be https, with an exemption for a loopback host (local dev has no
        // way to get valid TLS for localhost).
        //
        // Known gap, deliberately not closed here: this also does not compare port, so it is not a
        // strict RFC 6454 same-origin check -- a listener on the same host but a different port
        // (e.g. a debug endpoint or misconfigured sidecar) would pass. Left open because requestHost
        // comes from HostString.Host, which never carries a port, and normalizing "no port" against
        // an implicit default (443) would be wrong for a deployment genuinely fronted on a
        // non-standard port -- nginx's $host (see nginx.conf's X-Forwarded-Host) doesn't preserve
        // one either, so there's no reliable signal to compare against without broader changes to
        // that trust chain. The load-bearing fix for host spoofing is nginx.conf always overwriting
        // X-Forwarded-Host with $host rather than passing a client-supplied one through.
        internal static bool IsSameOriginRedirectUri(Uri redirectUri, string requestHost)
            => string.Equals(redirectUri.Host, requestHost, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(redirectUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || IsLoopbackHost(redirectUri.Host));

        // "api" is only ever used for its one fixed self-loopback OIDC callback path -- see the
        // longer rationale at the AddServer(...) call site in Main.
        internal static bool IsApiRedirectUri(Uri redirectUri, string requestHost)
            => IsSameOriginRedirectUri(redirectUri, requestHost)
                && string.Equals(redirectUri.AbsolutePath, "/login/signin-oidc", StringComparison.Ordinal);

        // "app" (the PWA) always redirects to exactly "<origin>/" -- verified directly against
        // oidc-spa's source, see the longer rationale at the AddServer(...) call site in Main.
        // Requiring the exact path (not just the origin) closes off tenant-controlled same-origin
        // paths like /share/{app}/{port} as redirect targets.
        internal static bool IsAppRedirectUri(Uri redirectUri, string requestHost)
            => IsSameOriginRedirectUri(redirectUri, requestHost)
                && string.Equals(redirectUri.AbsolutePath, "/", StringComparison.Ordinal);

        internal static async Task<bool> IsRegisteredLoopbackAwareRedirectUriAsync(
            IOpenIddictApplicationManager applicationManager, string clientId, Uri redirectUri)
        {
            var application = await applicationManager.FindByClientIdAsync(clientId);
            if (application is null)
            {
                return false;
            }

            foreach (var registered in await applicationManager.GetRedirectUrisAsync(application))
            {
                if (!Uri.TryCreate(registered, UriKind.Absolute, out var registeredUri))
                {
                    continue;
                }

                var sameScheme = string.Equals(registeredUri.Scheme, redirectUri.Scheme, StringComparison.OrdinalIgnoreCase);
                var sameHost = string.Equals(registeredUri.Host, redirectUri.Host, StringComparison.OrdinalIgnoreCase);
                var samePath = string.Equals(registeredUri.AbsolutePath, redirectUri.AbsolutePath, StringComparison.Ordinal);

                if (!sameScheme || !sameHost || !samePath)
                {
                    continue;
                }

                if (IsLoopbackHost(registeredUri.Host))
                {
                    return true;
                }

                if (registeredUri.Port == redirectUri.Port)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
