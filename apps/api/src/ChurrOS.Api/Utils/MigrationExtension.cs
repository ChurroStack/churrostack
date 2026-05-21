using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Domain.Auth;
using ChurrOS.Api.Jobs;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using OpenIddict.Abstractions;
using Quartz;
using System.Reflection;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ChurrOS.Api.Utils
{
    public static class MigrationExtension
    {
        public static async Task CreateAccountAsync(IConfiguration configuration, ISchedulerFactory schedulerFactory, IIdGeneratorService idGeneratorService, TemplateService templateService, ChurrosDbContext context, string domain, string[]? owners)
        {
            // Generate account encryption key and save it using master key)
            var masterKey = configuration["MasterKey"]!;
            var accountKey = AesGcmEncryption.GenerateBase64Key();
            var accountIv = AesGcmEncryption.GenerateBase64Iv();
            var encryptedAccountKey = AesGcmEncryption.Encrypt(accountKey, masterKey, accountIv);
            var accountId = idGeneratorService.CreateLongId();

            long networkQuota = string.IsNullOrWhiteSpace(configuration["Quota:Network"]) ? 25 : long.Parse(configuration["Quota:Network"]!);
            networkQuota = networkQuota * 1024 * 1024 * 1024; // Convert to bytes
            long environmentsQuota = string.IsNullOrWhiteSpace(configuration["Quota:Environments"]) ? 3 : long.Parse(configuration["Quota:Environments"]!);
            long applicationsQuota = string.IsNullOrWhiteSpace(configuration["Quota:Applications"]) ? 15 : long.Parse(configuration["Quota:Applications"]!);

            // Add account entity
            context.Set<Account>().Add(new Account(accountId, "Default Account", [domain], owners ?? [], $"{encryptedAccountKey}:{accountIv}", new Dictionary<string, object>
            {
                {
                  "quotas", new Dictionary<string, object>
                    {
                        { "network", networkQuota },
                        { "environment", environmentsQuota },
                        { "applications", applicationsQuota }
                    }
                }
            }));

            // Create system and owner identities
            var now = DateTime.Now;
            long systemId = idGeneratorService.CreateLongId();
            long usersId = idGeneratorService.CreateLongId();
            context.Set<Identity>().Add(new Identity(accountId, systemId, "system", "System", Models.Dtos.Identity.IdentityType.Application, Models.Dtos.Identity.IdentityRole.User, now, null, now, null));
            context.Set<Identity>().Add(new Identity(accountId, usersId, "users", "Users", Models.Dtos.Identity.IdentityType.Group, Models.Dtos.Identity.IdentityRole.User, now, null, now, null));
            foreach (var owner in owners ?? [])
            {
                var ownerId = idGeneratorService.CreateLongId();
                context.Set<Identity>().Add(new Identity(accountId, ownerId, owner.ToLowerInvariant(), owner, Models.Dtos.Identity.IdentityType.User, Models.Dtos.Identity.IdentityRole.Administrator, now, systemId, now, systemId));
                context.Set<IdentityMemberOf>().Add(new IdentityMemberOf(accountId, ownerId, usersId));
            }

            IDictionary<string, long> categories = new Dictionary<string, long>();

            // Add default templates
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.Extensions.terminal-extension.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.Extensions.file-browser-extension.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.Extensions.git-extension.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.Extensions.storage-extension.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.Extensions.gpu-extension.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.streamlit-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.streamlit-x11-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.vllm-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.fastapi-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.fastapi-x11-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.fastmcp-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.qdrant-application.yaml", categories);
            await AddTemplateAsync(accountId, systemId, context, templateService, idGeneratorService, "Resources.Templates.docker-application.yaml", categories);

            // Commit changes to database
            await context.SaveChangesAsync();

            // Reset quota monthly job
            int dayOfMonth = now.Day;
            int hour = now.Hour;
            int minute = now.Minute;
            var quotaJobIdentity = $"account-quota-job";
            var quotaJobTrigger = TriggerBuilder.Create()
                .WithIdentity(quotaJobIdentity)
                .UsingJobData("accountId", accountId.ToString())
                .StartNow()
                .WithCronSchedule(
                    $"0 {minute} {hour} {dayOfMonth} * ?",
                    x => x.WithMisfireHandlingInstructionFireAndProceed()
                )
                .Build();
            var quotaJob = JobBuilder.Create<QuotaResetJob>()
                .WithIdentity(quotaJobIdentity)
                .Build();
            var scheduler = await schedulerFactory.GetScheduler();
            await scheduler.ScheduleJob(quotaJob, quotaJobTrigger);
            await scheduler.TriggerJob(quotaJob.Key, new JobDataMap() { { "accountId", accountId.ToString() } });
        }

        public static async Task RegisterApplications(IServiceProvider serviceProvider)
        {
            var appManager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var scopeManager = serviceProvider.GetRequiredService<IOpenIddictScopeManager>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            // API
            await RegisterAPIApplication(appManager, scopeManager, "api",
                scopes: ["api/.default"],
                allowedRequestScopes: ["api/.default"],
                configuration["ClientSecret"]!,
                allowClientCredentialsFlow: false);

            // PWA/Native App
            await RegisterPwaNativeApplication(appManager, "app", ["api/.default"]);
        }

        public static async Task InitilizeTunnelUser(IConfiguration configuration, ChurrosDbContext context)
        {
            if (!await context.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'tunnel' );"))
            {
                await context.ExecuteScalarAsync<object?>($"CREATE ROLE tunnel LOGIN PASSWORD '{configuration["Tunnel:PgsqlPassword"]}';");
                await context.ExecuteScalarAsync<object?>("REVOKE ALL ON cs.environment FROM tunnel;");
            }
            await context.ExecuteScalarAsync<object?>("GRANT SELECT (port, ssh_public_key) ON cs.environment TO tunnel;");
            await context.ExecuteScalarAsync<object?>("GRANT USAGE ON SCHEMA cs TO tunnel;");
        }

        private static async Task AddTemplateAsync(long accountId, long identityId, ChurrosDbContext context, TemplateService templateService, IIdGeneratorService idGeneratorService, string resourceName, IDictionary<string, long> categories)
        {
            var rawTemplate = Assembly.GetExecutingAssembly().ReadResourceAsString(resourceName);
            var hash = rawTemplate.GetSha1Hash();
            var templateJson = await templateService.EvaluateAsync(rawTemplate);
            var template = templateJson.Deserialize<TemplateDefinition>(JsonSettings.Value)!;
            var now = DateTimeOffset.UtcNow;
            var metadata = JsonElement.Parse("{}");

            if (!categories.TryGetValue(template.Category!.Name, out var categoryId))
            {
                context.Set<TemplateCategory>().Add(new TemplateCategory(accountId, categoryId = idGeneratorService.CreateLongId(), template.Category.Name, template.Category.Title, template.Category.Icon, template.Category.Translation));
                categories.Add(template.Category.Name, categoryId);
            }

            context.Set<Template>().Add(new Template(accountId, idGeneratorService.CreateLongId(), categoryId, hash, template, rawTemplate, metadata, now, identityId, now, identityId));
        }

        private static async Task RegisterAPIApplication(IOpenIddictApplicationManager appManager, IOpenIddictScopeManager scopeManager, string clientId, string[] scopes, string[] allowedRequestScopes, string clientSecret, bool allowClientCredentialsFlow)
        {
            if (await appManager.FindByClientIdAsync(clientId) is null)
            {
                // Create application scope
                foreach (var scope in scopes)
                {
                    await scopeManager.CreateAsync(new OpenIdScope
                    {
                        Name = scope,
                        DisplayName = scope,
                        // Create resource (audience)
                        Resources = JsonSerializer.Serialize(new[] { clientId }, JsonSettings.Value)
                    });
                }

                // Register application
                var app = new OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    DisplayName = clientId,
                    ApplicationType = ApplicationTypes.Web,
                    ClientType = ClientTypes.Confidential,
                    ConsentType = ConsentTypes.Implicit,
                    //Settings =
                    //{
                    //    // Use a shorter access token lifetime for tokens issued to the Postman application.
                    //    [Settings.TokenLifetimes.AccessToken] = TimeSpan.FromMinutes(10).ToString("c", CultureInfo.InvariantCulture)
                    //}
                };
                foreach (var permission in new[] {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.ClientCredentials,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.GrantTypes.TokenExchange,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles
                }.Union(scopes.Union(allowedRequestScopes).Distinct().Select(o => Permissions.Prefixes.Scope + o)).Distinct())
                {
                    if (permission == Permissions.GrantTypes.ClientCredentials && !allowClientCredentialsFlow)
                        continue;
                    app.Permissions.Add(permission);
                }
                await appManager.CreateAsync(app);
            }
        }

        private static async Task RegisterPwaNativeApplication(IOpenIddictApplicationManager appManager, string clientId, string[] apiScopesId)
        {
            if (await appManager.FindByClientIdAsync(clientId) is null)
            {
                // Register application
                var app = new OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    DisplayName = clientId,
                    ApplicationType = ApplicationTypes.Native,
                    ClientType = ClientTypes.Public,
                    ConsentType = ConsentTypes.Implicit,
                    //RedirectUris =
                    //{
                    //    new Uri($"https://identity-manager.local")
                    //},
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    },
                    //Settings =
                    //{
                    //    // Use a shorter access token lifetime for tokens issued to the Postman application.
                    //    [Settings.TokenLifetimes.AccessToken] = TimeSpan.FromMinutes(10).ToString("c", CultureInfo.InvariantCulture)
                    //}
                };

                foreach (var permission in new[] {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.EndSession,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles
                }.Union(apiScopesId.Select(o => Permissions.Prefixes.Scope + o)).Distinct())
                {
                    app.Permissions.Add(permission);
                }

                await appManager.CreateAsync(app);
            }
        }
    }
}
