using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>
    /// Runs one Entra sync for the current tenant: fetch (read-only, cancellable) → plan (pure) → apply (single transaction).
    /// Progress, cancellation and the outcome live in Redis (<see cref="EntraSyncConfigStore.StatusKey"/> / <see cref="EntraSyncConfigStore.CancelKey"/>).
    /// Deliberately does not use UpsertIdentity: that handler needs an admin caller, resets roles and replaces memberships.
    /// </summary>
    public class EntraSyncService
    {
        private const int MaxWarnings = 20;
        private static readonly TimeSpan StatusTtl = TimeSpan.FromDays(30);
        private static readonly TimeSpan CancelTtl = TimeSpan.FromHours(1);

        private readonly ChurrosDbContext _dbContext;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;
        private readonly GraphDirectoryClient _graph;
        private readonly IIdGeneratorService _idGenerator;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EntraSyncService> _logger;

        public EntraSyncService(ChurrosDbContext dbContext, ITenantResolver tenantResolver, ICacheService cacheService, GraphDirectoryClient graph, IIdGeneratorService idGenerator, IConfiguration configuration, ILogger<EntraSyncService> logger)
        {
            _dbContext = dbContext;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
            _graph = graph;
            _idGenerator = idGenerator;
            _configuration = configuration;
            _logger = logger;
        }

        private sealed class CancelRequestedException : Exception { }

        public async Task RunAsync(long accountId, string trigger, CancellationToken cancellationToken)
        {
            var status = new EntraSyncStatus
            {
                RunId = Guid.NewGuid().ToString("N")[..12],
                State = EntraSyncState.Running,
                Phase = "Starting",
                Trigger = trigger,
                StartedAt = DateTimeOffset.UtcNow
            };
            var statusKey = EntraSyncConfigStore.StatusKey(accountId);
            var cancelKey = EntraSyncConfigStore.CancelKey(accountId);
            var lastWrite = DateTimeOffset.MinValue;

            async Task SaveStatus(bool force = false)
            {
                var now = DateTimeOffset.UtcNow;
                if (!force && now - lastWrite < TimeSpan.FromSeconds(1)) return;
                lastWrite = now;
                status.HeartbeatAt = now;
                await _cacheService.SaveAsync(statusKey, status, StatusTtl, CancellationToken.None);
            }

            async Task Checkpoint(string phase, int processed, int? total)
            {
                status.Phase = phase;
                status.Processed = processed;
                status.Total = total;
                if (await _cacheService.GetAsync<string>(cancelKey, cancellationToken) is not null)
                    throw new CancelRequestedException();
                await SaveStatus();
            }

            _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} trigger={Trigger} outcome=started", accountId, status.RunId, trigger);
            try
            {
                // Manual runs: StartEntraSync already cleared the flag, so a Stop pressed while queued is honoured at the first checkpoint.
                // Scheduled runs are never "queued" in the UI, so clear any stale flag left by an interrupted run.
                if (trigger == "schedule") await _cacheService.InvalidateAsync(cancelKey);
                await SaveStatus(force: true);

                var account = await _dbContext.Set<Domain.Account>().AsNoTracking().Where(o => o.Id == accountId).Select(o => new { o.Metadata }).SingleAsync(cancellationToken);
                var config = EntraSyncConfigStore.Read(account.Metadata);
                var tenantId = GraphDirectoryClient.ConfiguredTenantId(_configuration);
                if (!GraphDirectoryClient.IsConfigured(_configuration) || tenantId is null)
                    throw new EntraSyncException("Entra sync is not configured on the server (ExternalProviders:Microsoft ClientId, ClientSecret and TenantId).");

                _graph.UseTenant(tenantId);
                var filter = config.Filter.Enabled && config.Filter.Rules.Count > 0 ? config.Filter : null;
                var filterProperties = filter?.Rules.Select(o => o.Property) ?? Enumerable.Empty<string>();

                // ---- Fetch (read-only: a cancel or failure here leaves the database untouched) ----
                var users = new List<EntraUser>();
                await Checkpoint("Fetching users", 0, null);
                await foreach (var user in _graph.GetUsersAsync(filterProperties, cancellationToken))
                {
                    users.Add(user);
                    if (users.Count % 100 == 0) await Checkpoint("Fetching users", users.Count, null);
                }

                var groupHeaders = new List<(string Id, string? DisplayName)>();
                await Checkpoint("Fetching groups", users.Count, null);
                await foreach (var group in _graph.GetGroupsAsync(cancellationToken))
                    groupHeaders.Add(group);

                var groups = new List<EntraGroup>(groupHeaders.Count);
                for (var i = 0; i < groupHeaders.Count; i++)
                {
                    await Checkpoint("Resolving group members", i, groupHeaders.Count);
                    var members = new HashSet<string>(StringComparer.Ordinal);
                    await foreach (var memberId in _graph.GetGroupUserMemberIdsAsync(groupHeaders[i].Id, cancellationToken))
                        members.Add(memberId);
                    groups.Add(new EntraGroup(groupHeaders[i].Id, groupHeaders[i].DisplayName, members));
                }

                _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} fetched users={Users} groups={Groups}", accountId, status.RunId, users.Count, groups.Count);

                // ---- Plan (pure) ----
                await Checkpoint("Applying changes", groups.Count, groups.Count);
                var local = await LoadLocalSnapshotAsync(cancellationToken);
                var now = DateTimeOffset.UtcNow;
                var changes = EntraSyncPlanner.Plan(local, new EntraSnapshot(users, groups), filter, _idGenerator.CreateLongId, now);

                // ---- Apply (atomic, not cancellable) ----
                await ApplyAsync(accountId, local, changes, now, cancellationToken);

                status.State = EntraSyncState.Succeeded;
                status.Phase = "Done";
                status.Summary = changes.Stats.ToSummary();
                status.Warnings = changes.Warnings.Take(MaxWarnings).ToList();
                status.FinishedAt = DateTimeOffset.UtcNow;
                await SaveStatus(force: true);
                _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} outcome=succeeded matchedUsers={Matched} {Summary} warnings={Warnings}",
                    accountId, status.RunId, changes.Stats.MatchedUsers, status.Summary, changes.Warnings.Count);
            }
            catch (CancelRequestedException)
            {
                await Finish(EntraSyncState.Cancelled, "Stopped by an administrator.");
                _logger.LogInformation("[EntraSync] accountId={AccountId} runId={RunId} outcome=cancelled", accountId, status.RunId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await Finish(EntraSyncState.Cancelled, "Interrupted (service is shutting down).");
                _logger.LogWarning("[EntraSync] accountId={AccountId} runId={RunId} outcome=cancelled reason=shutdown", accountId, status.RunId);
            }
            catch (EntraSyncException ex)
            {
                await Finish(EntraSyncState.Failed, ex.Message);
                _logger.LogWarning("[EntraSync] accountId={AccountId} runId={RunId} outcome=failed error={Error}", accountId, status.RunId, ex.Message);
            }
            catch (Exception ex)
            {
                var message = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message;
                await Finish(EntraSyncState.Failed, $"Unexpected error: {message}");
                _logger.LogError(ex, "[EntraSync] accountId={AccountId} runId={RunId} outcome=failed", accountId, status.RunId);
            }
            finally
            {
                await _cacheService.InvalidateAsync(cancelKey);
            }

            async Task Finish(string state, string error)
            {
                status.State = state;
                status.Error = error;
                status.FinishedAt = DateTimeOffset.UtcNow;
                await SaveStatus(force: true);
            }
        }

        private async Task<LocalSnapshot> LoadLocalSnapshotAsync(CancellationToken cancellationToken)
        {
            var identities = await _dbContext.Set<Domain.Identity>().AsNoTracking()
                .Select(o => new LocalIdentity(o.Id, o.Name, o.DisplayName, o.Type, o.Source, o.ExternalId, o.LockAfter))
                .ToListAsync(cancellationToken);
            var memberships = await _dbContext.Set<Domain.IdentityMemberOf>().AsNoTracking()
                .Select(o => new LocalMembership(o.IdentityId, o.GroupId, o.Source))
                .ToListAsync(cancellationToken);
            return new LocalSnapshot(identities, memberships);
        }

        private async Task ApplyAsync(long accountId, LocalSnapshot local, SyncChanges changes, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var actorId = _dbContext.IdentityId; // "system"
            long? actor = actorId > 0 ? actorId : null;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var identityRepo = _dbContext.Set<Domain.Identity>();
            var membershipRepo = _dbContext.Set<Domain.IdentityMemberOf>();

            var updateIds = changes.Updates.Select(o => o.Id).ToArray();
            var tracked = updateIds.Length == 0
                ? new Dictionary<long, Domain.Identity>()
                : await identityRepo.Where(o => updateIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken);

            foreach (var update in changes.Updates)
            {
                if (!tracked.TryGetValue(update.Id, out var identity)) continue;
                if (update.NewName is not null) identity.SetName(update.NewName);
                if (update.NewDisplayName is not null) identity.SetDisplayName(update.NewDisplayName);
                if (update.LinkExternalId is not null) identity.LinkExternal(update.LinkExternalId);
                if (update.Enable) identity.Enable();
                if (update.Disable) identity.Disable(now);
                if (actor.HasValue) identity.SetModified(actor.Value, now);
                else identity.ModifiedAt = now;
            }

            foreach (var create in changes.Creates)
            {
                var identity = new Domain.Identity(accountId, create.Id, create.Name, create.DisplayName, create.Type, IdentityRole.User, now, actor, now, actor);
                identity.SetSource(IdentitySource.Entra);
                identity.LinkExternal(create.ExternalId);
                identityRepo.Add(identity);
            }

            foreach (var add in changes.MembershipAdds)
                membershipRepo.Add(new Domain.IdentityMemberOf(accountId, add.IdentityId, add.GroupId, IdentitySource.Entra));

            if (changes.MembershipRemoves.Count > 0)
            {
                var removeSet = changes.MembershipRemoves.ToHashSet();
                // Rows of groups deleted below are removed by ExecuteDelete; tracking them would make EF cascade-delete them a second time.
                var deletedGroups = changes.DeleteIds.ToArray();
                var entraRows = await membershipRepo.Where(o => o.Source == IdentitySource.Entra && !deletedGroups.Contains(o.GroupId)).ToListAsync(cancellationToken);
                membershipRepo.RemoveRange(entraRows.Where(o => removeSet.Contains(new MembershipKey(o.IdentityId, o.GroupId))));
            }

            if (changes.DeleteIds.Count > 0)
            {
                var deleteIds = changes.DeleteIds.ToArray();
                await membershipRepo.Where(o => deleteIds.Contains(o.GroupId)).ExecuteDeleteAsync(cancellationToken);
                identityRepo.RemoveRange(await identityRepo.Where(o => deleteIds.Contains(o.Id)).ToListAsync(cancellationToken));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await InvalidateCachesAsync(accountId, local, changes, cancellationToken);
        }

        /// <summary>Mirrors UpsertIdentity: purge per-identity caches (by name and by id, including members of changed groups).</summary>
        private async Task InvalidateCachesAsync(long accountId, LocalSnapshot local, SyncChanges changes, CancellationToken cancellationToken)
        {
            var ids = new HashSet<long>(changes.Updates.Select(o => o.Id));
            ids.UnionWith(changes.Creates.Select(o => o.Id));
            ids.UnionWith(changes.DeleteIds);
            foreach (var key in changes.MembershipAdds.Concat(changes.MembershipRemoves))
            {
                ids.Add(key.IdentityId);
                ids.Add(key.GroupId);
            }
            // members of deleted groups lose the group
            foreach (var membership in local.Memberships.Where(o => changes.DeleteIds.Contains(o.GroupId)))
                ids.Add(membership.IdentityId);

            var names = local.Identities.Where(o => ids.Contains(o.Id)).Select(o => o.Name)
                .Concat(changes.Creates.Select(o => o.Name))
                .Concat(changes.Updates.Where(o => o.NewName is not null).Select(o => o.NewName!))
                .Distinct();
            foreach (var name in names)
            {
                await _cacheService.InvalidatePrefixAsync($"identity:{name}:tenant:default");
                await _cacheService.InvalidatePrefixAsync($"tenant:{accountId}:identity:{name}");
            }

            var survivingIds = ids.Except(changes.DeleteIds).ToArray();
            if (survivingIds.Length > 0)
                await _cacheService.InvalidateIdentityAuthorizationCachesAsync(_dbContext, accountId, survivingIds, _logger, cancellationToken);
            foreach (var id in changes.DeleteIds)
                await _cacheService.InvalidatePrefixAsync($"tenant:{accountId}:identity:{id}");
        }
    }
}
