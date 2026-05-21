using ChurrOS.Api.Domain;
using ChurrOS.Api.Domain.Auth;
using ChurrOS.Api.Models.Dtos.Account;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using LazyCache;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using System.Text.Json;

namespace ChurrOS.Api.Data
{
    public class ChurrosDbContext : IdentityDbContext<OpenIdUser, OpenIdRole, Guid, OpenIdUserClaim, OpenIdUserRole, OpenIdUserLogin, OpenIdRoleClaim, OpenIdUserToken, OpenIdUserPasskey>
    {
        private readonly ITenantResolver _tenantResolver;
        private readonly IAppCache _cacheService;
        private readonly IConfiguration _configuration;
        private long AccountId
        {
            get
            {
                try
                {
                    return _tenantResolver.AccountId;
                }
                catch
                {
                    return 0;
                }
            }
        }
        public long IdentityId => GetIdentityId();
        public string AccountEncryptionKey => GetAccountEncryptionKey();
        public string[] Domains => GetTenant().Domains;
        public string[] Owners => GetTenant().Owners;

        public AccountQuotaItem Quota
        {
            get
            {
                var tenant = GetTenant();
                long networkQuota = string.IsNullOrWhiteSpace(_configuration["Quota:Network"]) ? 25 : long.Parse(_configuration["Quota:Network"]!);
                networkQuota = networkQuota * 1024 * 1024 * 1024; // Convert to bytes
                long environmentsQuota = string.IsNullOrWhiteSpace(_configuration["Quota:Environments"]) ? 3 : long.Parse(_configuration["Quota:Environments"]!);
                long applicationsQuota = string.IsNullOrWhiteSpace(_configuration["Quota:Applications"]) ? 15 : long.Parse(_configuration["Quota:Applications"]!);
                if (tenant.Metadata?.TryGetValue("quotas", out var jsonQuotas) ?? false)
                {
                    if (((JsonElement)jsonQuotas).TryGetProperty("environments", out var jsonEnv))
                    {
                        environmentsQuota = jsonEnv.GetInt64();
                    }
                    if (((JsonElement)jsonQuotas).TryGetProperty("applications", out var jsonApps))
                    {
                        applicationsQuota = jsonApps.GetInt64();
                    }
                    if (((JsonElement)jsonQuotas).TryGetProperty("network", out var jsonNetwork))
                    {
                        networkQuota = jsonNetwork.GetInt64();
                    }
                }
                return new AccountQuotaItem(
                    network: networkQuota,
                    environments: environmentsQuota,
                    applications: applicationsQuota
                );
            }
        }

        public ChurrosDbContext(DbContextOptions<ChurrosDbContext> options, ITenantResolver tenantResolver, IAppCache cacheService, IConfiguration configuration) : base(options)
        {
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
            _configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChurrosDbContext).Assembly);
            modelBuilder.Entity<Acl>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<AclMember>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<Domain.Environment>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<Template>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<TemplateCategory>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<Identity>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<IdentityMemberOf>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<Application>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<ApplicationExtension>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<ApplicationEvent>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<ApplicationSchedule>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<Metric>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<MetricValue>().HasQueryFilter(mt => mt.AccountId == AccountId);
            modelBuilder.Entity<ApplicationTrace>().HasQueryFilter(mt => mt.AccountId == AccountId);
        }

        public async Task<T?> ExecuteScalarAsync<T>(FormattableString query)
        {
            var connection = Database.GetDbConnection();

            var isOpen = connection.State == System.Data.ConnectionState.Open;

            if (!isOpen)
                await connection.OpenAsync();


            try
            {
                var @params = new List<object>();
                foreach (var item in query.GetArguments())
                {
                    if (item is IEnumerable enumerable)
                    {
                        var parts = new List<string>();
                        foreach (var part in enumerable)
                        {
                            if (part is not null)
                            {
                                if (part is string)
                                {
                                    parts.Add($"'{part.ToString()}'");
                                }
                                else
                                {
                                    parts.Add(part.ToString());
                                }
                            }
                        }
                        @params.Add($"ARRAY[{string.Join(",", parts)}]");
                    }
                    else
                    {
                        @params.Add(item);
                    }
                }

                using var command = connection.CreateCommand();
                command.CommandText = string.Format(query.Format, @params.ToArray());
                var result = await command.ExecuteScalarAsync();
                return (T?)result;
            }
            finally
            {
                if (!isOpen)
                    await connection.CloseAsync();
            }
        }

        public async Task<T?> ExecuteScalarAsync<T>(string query)
        {
            var connection = Database.GetDbConnection();

            var isOpen = connection.State == System.Data.ConnectionState.Open;

            if (!isOpen)
                await connection.OpenAsync();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = query;
                var result = await command.ExecuteScalarAsync();
                return (T?)result;
            }
            finally
            {
                if (!isOpen)
                    await connection.CloseAsync();
            }
        }

        public async Task<IList<T>> ExecuteQueryAsync<T>(FormattableString query)
        {
            return await Database.SqlQuery<T>(query).ToListAsync<T>();
        }

        private string GetAccountEncryptionKey()
        {
            var rawEncryptionKey = GetTenant().EncryptionKey;
            if (string.IsNullOrWhiteSpace(rawEncryptionKey))
                throw new ArgumentException("Account not found");
            var parts = rawEncryptionKey.Split(':', 2);
            var masterKey = _configuration["MasterKey"]!;
            return AesGcmEncryption.Decrypt(parts[0], masterKey, parts[1]);
        }

        private Account GetTenant()
        {
            var accountId = _tenantResolver.AccountId;
            return _cacheService.GetOrAdd($"tenant:{accountId}:account", entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                var tenantInfo = Set<Domain.Account>().AsNoTracking().Where(o => o.Id == accountId).FirstOrDefault();
                if (tenantInfo == null)
                    throw new UnauthorizedAccessException(LocalizationService.GetString("TenantNotFound"));
                return tenantInfo;
            });
        }

        private long GetIdentityId()
        {
            var accountId = _tenantResolver.AccountId;
            var identityName = _tenantResolver.Identity?.Name;
            if (string.IsNullOrWhiteSpace(identityName))
                return 0;

            return _cacheService.GetOrAdd($"tenant:{accountId}:identity:{identityName}", entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                var result = Set<Identity>().AsNoTracking().Where(o => o.Name.Equals(identityName)).Select(o => (long?)o.Id).FirstOrDefault();
                if (result is null)
                {
                    entry.SetAbsoluteExpiration(TimeSpan.FromSeconds(10));
                }
                return result ?? 0;
            });
        }
    }
}
