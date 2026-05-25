using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Applications
{
    public class AnalyzeApplicationUsageHandler : IRequestHandler<AnalyzeApplicationUsage, ValueTask<AnalyzeUsageResultItem>>
    {
        private const int WindowDays = 7;

        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly ITenantResolver _tenantResolver;
        private readonly ILogger<AnalyzeApplicationUsageHandler> _logger;

        public AnalyzeApplicationUsageHandler(
            ChurrosDbContext context,
            IMediator mediator,
            ITenantResolver tenantResolver,
            ILogger<AnalyzeApplicationUsageHandler> logger)
        {
            _context = context;
            _mediator = mediator;
            _tenantResolver = tenantResolver;
            _logger = logger;
        }

        public async ValueTask<AnalyzeUsageResultItem> Handle(AnalyzeApplicationUsage request, CancellationToken cancellationToken)
        {
            var accountId = _tenantResolver.AccountId;

            // Resolve the environment scope (also used for the permission check).
            long? scopedEnvironmentAclId = null;
            if (!string.IsNullOrWhiteSpace(request.EnvironmentName))
            {
                scopedEnvironmentAclId = await _context.Set<Domain.Environment>()
                    .Where(e => e.Name == request.EnvironmentName)
                    .Select(e => (long?)e.AclId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (scopedEnvironmentAclId == null)
                    throw new NotFoundException($"Environment with name '{request.EnvironmentName}' was not found.");
            }

            // Resolve the target applications.
            var appsQuery = _context.Set<Domain.Application>().AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.ApplicationName))
                appsQuery = appsQuery.Where(a => a.Name == request.ApplicationName);
            if (!string.IsNullOrWhiteSpace(request.EnvironmentName))
                appsQuery = appsQuery.Where(a => a.Environment!.Name == request.EnvironmentName);

            var apps = await appsQuery
                .Select(a => new TargetApp(
                    a.Id, a.Name, a.AclId, a.Size, a.Environment!.AclId, a.Environment.Definition))
                .ToListAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.ApplicationName) && apps.Count == 0)
                throw new NotFoundException($"Application with name '{request.ApplicationName}' was not found.");

            await EnsureAuthorizedAsync(request, apps, scopedEnvironmentAclId, cancellationToken);

            _logger.LogInformation(
                "AnalyzeApplicationUsage: starting account={AccountId} app={Application} environment={Environment} targets={Targets}",
                accountId, request.ApplicationName ?? "(all)", request.EnvironmentName ?? "(all)", apps.Count);

            var appIds = apps.Select(a => a.Id).ToList();
            var existing = await _context.Set<ApplicationSizeRecommendation>()
                .Where(r => appIds.Contains(r.ApplicationId))
                .ToDictionaryAsync(r => r.ApplicationId, cancellationToken);

            var fromDate = DateTimeOffset.UtcNow.AddDays(-WindowDays);

            // Aggregate CPU/memory samples for every target application in a single
            // round trip; avoids the previous N+1 (2 queries per app).
            var usageByApp = await ComputeUsageBatchAsync(accountId, appIds.ToArray(), fromDate);

            var analyzed = 0;
            var recommendations = 0;

            foreach (var app in apps)
            {
                try
                {
                    var stats = usageByApp.TryGetValue(app.Id, out var s) ? s : UsageStatRow.Empty;
                    var sampleCount = (int)Math.Min(stats.CpuCount, stats.MemCount);

                    SizeRequestItem? recommended = null;
                    if (stats.CpuCount >= SizeRecommendation.MinSampleCount &&
                        stats.MemCount >= SizeRecommendation.MinSampleCount)
                    {
                        var cpuBasis = stats.CpuP95 * SizeRecommendation.CpuHeadroom;
                        var memoryBasis = stats.MemMax * SizeRecommendation.MemoryHeadroom;
                        recommended = SizeRecommendation.PickSize(app.Definition?.Sizes, app.Size, cpuBasis, memoryBasis);
                    }

                    if (!existing.TryGetValue(app.Id, out var recommendation))
                    {
                        recommendation = new ApplicationSizeRecommendation(accountId, app.Id);
                        _context.Add(recommendation);
                        existing[app.Id] = recommendation;
                    }

                    recommendation.CpuAvg = stats.CpuAvg;
                    recommendation.CpuMax = stats.CpuMax;
                    recommendation.CpuP95 = stats.CpuP95;
                    recommendation.MemoryAvg = stats.MemAvg;
                    recommendation.MemoryMax = stats.MemMax;
                    recommendation.MemoryP95 = stats.MemP95;
                    recommendation.SampleCount = sampleCount;
                    recommendation.WindowDays = WindowDays;
                    recommendation.RecommendedSize = recommended;
                    recommendation.ComputedAt = DateTimeOffset.UtcNow;

                    analyzed++;
                    if (recommended != null &&
                        SizeRecommendation.GetDirection(app.Size, recommended) != SizeRecommendation.Optimal)
                        recommendations++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "AnalyzeApplicationUsage: failed for application {Application} (id {ApplicationId}).",
                        app.Name, app.Id);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "AnalyzeApplicationUsage: completed account={AccountId} analyzed={Analyzed} recommendations={Recommendations}",
                accountId, analyzed, recommendations);

            return new AnalyzeUsageResultItem
            {
                ApplicationsAnalyzed = analyzed,
                RecommendationsCount = recommendations,
            };
        }

        private async ValueTask EnsureAuthorizedAsync(
            AnalyzeApplicationUsage request,
            IReadOnlyList<TargetApp> apps,
            long? scopedEnvironmentAclId,
            CancellationToken cancellationToken)
        {
            if (request.SkipAuthorization)
                return;

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (isAdmin)
                return;

            var manageAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Manage), cancellationToken);

            bool allowed;
            if (!string.IsNullOrWhiteSpace(request.ApplicationName))
                allowed = apps.All(a => manageAcls.ContainsKey(a.AclId) || manageAcls.ContainsKey(a.EnvironmentAclId));
            else if (scopedEnvironmentAclId != null)
                allowed = manageAcls.ContainsKey(scopedEnvironmentAclId.Value);
            else
                allowed = false; // a tenant-wide analysis requires an administrator (or the nightly job)

            if (!allowed)
                throw new UnauthorizedAccessException("You do not have permission to analyze application usage.");
        }

        /// <summary>
        /// Aggregates CPU/memory gauge samples for every target application over the
        /// window in a single SQL round trip, returning <c>(applicationId → stats)</c>.
        /// Apps with no samples are omitted; callers should default them to
        /// <see cref="UsageStatRow.Empty"/>. Both metrics are gauges, so raw per-sample
        /// values are used directly.
        /// </summary>
        private async ValueTask<Dictionary<long, UsageStatRow>> ComputeUsageBatchAsync(
            long accountId, long[] applicationIds, DateTimeOffset fromDate)
        {
            if (applicationIds.Length == 0)
                return new Dictionary<long, UsageStatRow>();

            // One query: join cs.metric (resolves application_id + kind from labels) to
            // cs.metric_value (the gauge samples) and aggregate per (app, kind).
            // Column aliases match the snake_case form of AppKindStat's properties
            // (EFCore.NamingConventions); avoid a digit-to-letter break (e.g. "P95Value"
            // would map to "p95value", not "p95_value") by naming the property "ValueP95".
            var rows = await _context.ExecuteQueryAsync<AppKindStat>($@"
                SELECT
                    (m.labels ->> 'application_id')::bigint AS application_id,
                    m.labels ->> 'metric' AS metric_kind,
                    COALESCE(avg(mv.value), 0.0) AS value_avg,
                    COALESCE(max(mv.value), 0.0) AS value_max,
                    COALESCE(percentile_cont(0.95) WITHIN GROUP (ORDER BY mv.value), 0.0) AS value_p95,
                    count(mv.value) AS value_count
                FROM cs.metric m
                LEFT JOIN cs.metric_value mv
                    ON mv.metric_id = m.metric_id
                   AND mv.account_id = {accountId}
                   AND mv.timestamp >= {fromDate}
                WHERE m.account_id = {accountId}
                  AND (m.labels ->> 'application_id')::bigint = ANY({applicationIds})
                  AND m.labels ->> 'metric' IN ('cpu_usage', 'memory_usage')
                GROUP BY (m.labels ->> 'application_id')::bigint, m.labels ->> 'metric'");

            var result = new Dictionary<long, UsageStatRow>(applicationIds.Length);
            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.ApplicationId, out var current))
                    current = UsageStatRow.Empty;

                result[row.ApplicationId] = row.MetricKind == "cpu_usage"
                    ? current.WithCpu(row.ValueAvg, row.ValueMax, row.ValueP95, row.ValueCount)
                    : current.WithMemory(row.ValueAvg, row.ValueMax, row.ValueP95, row.ValueCount);
            }
            return result;
        }

        private sealed record TargetApp(
            long Id,
            string Name,
            long AclId,
            SizeRequestItem Size,
            long EnvironmentAclId,
            EnvironmentDefinition? Definition);
    }

    /// <summary>Aggregated CPU/memory usage statistics for a single application.</summary>
    internal sealed record UsageStatRow(
        double CpuAvg, double CpuMax, double CpuP95, long CpuCount,
        double MemAvg, double MemMax, double MemP95, long MemCount)
    {
        public static readonly UsageStatRow Empty = new(0, 0, 0, 0, 0, 0, 0, 0);

        public UsageStatRow WithCpu(double avg, double max, double p95, long count)
            => this with { CpuAvg = avg, CpuMax = max, CpuP95 = p95, CpuCount = count };

        public UsageStatRow WithMemory(double avg, double max, double p95, long count)
            => this with { MemAvg = avg, MemMax = max, MemP95 = p95, MemCount = count };
    }

    /// <summary>Per (application, metric kind) aggregation row returned by the batch query.</summary>
    internal sealed record AppKindStat(
        long ApplicationId, string MetricKind,
        double ValueAvg, double ValueMax, double ValueP95, long ValueCount);
}
