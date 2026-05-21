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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
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
            });

            builder.Services.Configure<FormOptions>(o =>
            {
                o.MultipartBodyLengthLimit = 10_000_000_000;
            });

            builder.Services
                .AddControllers(options =>
                {
                    options.Filters.Add(new ResponseExceptionFilter());
                    options.InputFormatters.Add(new Utils.AspNet.TextInputFormatter());
                })
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ApplyDefaultOptions();
                    JsonSettings.Value = options.JsonSerializerOptions;
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
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources/Locales");
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
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
                        policy.WithOrigins(builder.Configuration["Cors:Origins"]!.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        .AllowAnyHeader()
                        .AllowAnyMethod();
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

                    options.Configure(config => { config.Issuer = new Uri(builder.Configuration["BaseUrl"]!); });

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
                            }
                        });

                        builder.SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientRedirectUri.Descriptor.Order);
                    });


                    options.IgnoreScopePermissions();

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
            builder.Services.AddScoped<QuotaService, QuotaService>();
            builder.Services.AddSingleton<IIdGeneratorService, IdGenerationService>();
            builder.Services.AddSingleton<ICacheService, RedisCacheService>();
            builder.Services.AddSingleton<IQueueService, RedisStreamService>();
            builder.Services.AddSingleton<TemplateService, TemplateService>();
            builder.Services.AddSingleton<RunnerService, RunnerService>();
            builder.Services.AddSingleton<ClientNotificationService, ClientNotificationService>();
            builder.Services.AddSingleton<ProxyConfigurationProvider>();
            builder.Services.AddSingleton<IAuthorizationHandler, ApplicationMemberAccessHandler>();
            builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<ProxyConfigurationProvider>());
            builder.Services.AddSingleton<MetricsAggregatorService, MetricsAggregatorService>();
            builder.Services.AddHostedService<TracesProcessorJob>();
            builder.Services.AddReverseProxy();
            // Interface that collects general metrics about the proxy forwarder
            builder.Services.AddMetricsConsumer<ForwarderMetricsConsumer>();
            // Registration of a consumer to events for proxy forwarder telemetry
            builder.Services.AddTelemetryConsumer<ForwarderTelemetryConsumer>();
            // Registration of a consumer to events for HttpClient telemetry
            builder.Services.AddTelemetryConsumer<HttpClientTelemetryConsumer>();
            builder.Services.AddTelemetryConsumer<WebSocketsTelemetryConsumer>();

            var app = builder.Build();

            app.MapDefaultEndpoints();

            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapOpenApi();

#if !DEBUG
            app.UseHttpsRedirection();
#endif
            app.UseForwardedHeaders();
            app.Use((context, next) =>
            {
                context.Request.Scheme = "https";
                return next();
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
            app.UseAuthorization();
            app.MapHub<NotificationHub>("/api/notifications");
            app.MapControllers();

            var localizer = app.Services.GetRequiredService<IStringLocalizer<Locale>>();
            LocalizationService.Initialize(localizer);

            app.UsePerRequestMetricCollection();
            app.UseWebSocketsTelemetry();
            app.MapReverseProxy(pipeline =>
            {
                pipeline.Use((context, next) =>
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
                        return next();
                    }

                    var cacheService = context.RequestServices.GetService<ICacheService>()!;
                    var envInfo = cacheService.GetOrAddAsync($"env:{clusterId}:info", async (ctx) =>
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
                    }, CancellationToken.None).GetAwaiter().GetResult();

                    var tenantResolver = context.RequestServices.GetService<ITenantResolver>()!;
                    tenantResolver.SetAccountId(envInfo!.AccountId);
                    if (!string.IsNullOrWhiteSpace(context.User?.Identity?.Name))
                    {
                        tenantResolver.SetIdentity(context.User.Identity.Name);
                    }
                    var quotaService = context.RequestServices.GetService<QuotaService>()!;
                    try
                    {
                        quotaService.EnsureHasQuotaAsync(QuotaService.QuotaType.Network).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        context.Response.StatusCode = 429;
                        context.Response.WriteAsJsonAsync(new
                        {
                            error = ex.Message
                        });
                        return Task.CompletedTask;
                    }

                    if (context.Request.Path.StartsWithSegments($"/share"))
                    {
                        var parts = context.Request.Path.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
                        var appName = parts[1];

                        var (appMode, destination) = cacheService.GetOrAddAsync<RoutingInfo>($"app:{appName}:user:{context.User?.Identity?.Name}:routing_info", async (ctx) =>
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
                        }, CancellationToken.None).GetAwaiter().GetResult();

                        if (appMode == ApplicationMode.Workspace)
                        {
                            if (string.IsNullOrEmpty(destination))
                            {
                                context.Response.StatusCode = 404;
                                context.Response.WriteAsJsonAsync(new
                                {
                                    error = "Application deployment not found for the current user."
                                });
                                return Task.CompletedTask;
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

                    return next();
                });
            });

            // Database migration & seeding
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ChurrosDbContext>();
            context.Database.Migrate();
            await context.ApplyHypertablesAsync();
            await context.ApplyQuartzTables();

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
    }
}
