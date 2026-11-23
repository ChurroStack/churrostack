using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Entra;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Quartz;
using System.Text.RegularExpressions;

namespace ChurrOS.Api.Commands.IdentitySync
{
    public class UpsertEntraSyncConfigHandler : IRequestHandler<UpsertEntraSyncConfig, ValueTask<EntraSyncConfigItem>>
    {
        private static readonly Regex PropertyName = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.Compiled);
        private const int MaxRules = 20;
        private const int MaxValuesPerRule = 200;

        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly IMediator _mediator;
        private readonly ICacheService _cacheService;
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UpsertEntraSyncConfigHandler> _logger;

        public UpsertEntraSyncConfigHandler(ChurrosDbContext dbContext, ITenantResolver tenantResolver, IMediator mediator, ICacheService cacheService, ISchedulerFactory schedulerFactory, IConfiguration configuration, ILogger<UpsertEntraSyncConfigHandler> logger)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _mediator = mediator;
            _cacheService = cacheService;
            _schedulerFactory = schedulerFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async ValueTask<EntraSyncConfigItem> Handle(UpsertEntraSyncConfig request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var config = Normalize(request.Body);

            var accountId = _tenantResolver.AccountId;
            var account = await _dbContext.Set<Domain.Account>().SingleAsync(o => o.Id == accountId, cancellationToken);

            // The tenant is never taken from the request: it is the operator-set account.entraTenantId (or the server default).
            var tenantId = EntraSyncConfigStore.ResolveTenantId(account.Metadata, _configuration);
            Validate(config, tenantId);

            config.TenantId = null; // not stored under entraSync: the single source of truth is entraTenantId / server config
            account.SetContent(EntraSyncConfigStore.Merge(account.Metadata, config));
            await _dbContext.SaveChangesAsync(cancellationToken);
            config.TenantId = tenantId;

            // GetTenant() caches the Account for 1h; evict so quota/domain readers (and other config readers) see the new metadata.
            var accountCacheKey = $"tenant:{accountId}:account";
            await _cacheService.InvalidateAsync(accountCacheKey);
            _cacheService.Invalidate(accountCacheKey);

            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            await EntraSyncScheduling.ApplyScheduleAsync(scheduler, accountId, config, cancellationToken);

            _logger.LogInformation("[EntraSync] accountId={AccountId} outcome=config_saved scheduled={Scheduled} filterEnabled={FilterEnabled} rules={Rules}",
                accountId, config.Enabled, config.Filter.Enabled, config.Filter.Rules.Count);

            return new EntraSyncConfigItem { Config = config, Available = EntraSyncConfigStore.IsAvailable(account.Metadata, _configuration) };
        }

        private static EntraSyncConfig Normalize(EntraSyncConfig body) => new()
        {
            Enabled = body.Enabled,
            CronExpression = string.IsNullOrWhiteSpace(body.CronExpression) ? EntraSyncConfig.DefaultCron : body.CronExpression.Trim(),
            Filter = new EntraFilter
            {
                Enabled = body.Filter?.Enabled ?? false,
                Rules = (body.Filter?.Rules ?? new List<EntraFilterRule>())
                    .Select(r => new EntraFilterRule
                    {
                        Property = CamelCase(r.Property?.Trim() ?? string.Empty),
                        Values = (r.Values ?? Array.Empty<string>()).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    })
                    .ToList()
            }
        };

        // Graph user properties are camelCase (companyName); accept 'CompanyName' too.
        private static string CamelCase(string value) => value.Length == 0 ? value : char.ToLowerInvariant(value[0]) + value[1..];

        private static void Validate(EntraSyncConfig config, string? tenantId)
        {
            if (config.Enabled && tenantId is null)
                throw new ArgumentException("Entra sync is not configured: set the account's entraTenantId or ExternalProviders:Microsoft:TenantId.");

            EntraSyncScheduling.ToQuartzCron(config.CronExpression); // throws ArgumentException when invalid

            if (config.Filter.Enabled)
            {
                if (config.Filter.Rules.Count == 0)
                    throw new ArgumentException("Add at least one filter condition or turn the filter off.");
                if (config.Filter.Rules.Count > MaxRules)
                    throw new ArgumentException($"At most {MaxRules} filter conditions are allowed.");
            }

            foreach (var rule in config.Filter.Rules)
            {
                // The property is placed in Graph's $select: restrict it to plain identifiers.
                if (!PropertyName.IsMatch(rule.Property))
                    throw new ArgumentException($"Invalid filter property '{rule.Property}'.");
                if (rule.Values.Length == 0)
                    throw new ArgumentException($"Filter property '{rule.Property}' needs at least one value.");
                if (rule.Values.Length > MaxValuesPerRule)
                    throw new ArgumentException($"Filter property '{rule.Property}' has too many values.");
            }
        }
    }
}
