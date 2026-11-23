using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>
    /// Pure diff between the local identities and an Entra snapshot. No I/O, so it can be unit-tested.
    /// Rules (see docs/concepts/identity-sync.md):
    /// - users are never deleted, only disabled (LockAfter) because deleting an identity cascades to everything it created;
    /// - pre-existing local identities are linked (ExternalId) but never disabled/deleted and keep their role;
    /// - only memberships with Source=Entra are ever removed; admin-added (Local) memberships survive.
    /// </summary>
    public static class EntraSyncPlanner
    {
        public const string UsersGroupName = "users";
        private const int MaxNameLength = 255;

        public static string? UserName(EntraUser user)
        {
            var name = (!string.IsNullOrWhiteSpace(user.Mail) ? user.Mail : user.UserPrincipalName)?.Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        public static SyncChanges Plan(LocalSnapshot local, EntraSnapshot entra, EntraFilter? filter, Func<long> newId, DateTimeOffset now)
        {
            var changes = new SyncChanges();
            var stats = changes.Stats;

            var byExternalId = local.Identities.Where(o => o.ExternalId != null).ToDictionary(o => o.ExternalId!, StringComparer.Ordinal);
            var byName = local.Identities.ToDictionary(o => o.Name, StringComparer.Ordinal);
            var usedNames = new HashSet<string>(byName.Keys, StringComparer.Ordinal);
            var updates = new Dictionary<long, IdentityUpdate>();

            IdentityUpdate Update(long id)
            {
                if (!updates.TryGetValue(id, out var update))
                {
                    update = new IdentityUpdate(id);
                    updates[id] = update;
                }
                return update;
            }

            // ---- Users -----------------------------------------------------------------------------------------
            var eligible = new List<(EntraUser User, string Name)>();
            var eligibleIds = new HashSet<string>(StringComparer.Ordinal);
            // Users Entra still returns and the filter keeps, even if they are skipped below (no mail, duplicate name…):
            // those must never be disabled just because of a data-quality warning.
            var candidateIds = new HashSet<string>(StringComparer.Ordinal);
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var user in entra.Users.OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                if (!user.AccountEnabled || !EntraUserFilter.Matches(user.Properties, filter))
                    continue;

                candidateIds.Add(user.Id);
                var name = UserName(user);
                if (name is null)
                {
                    changes.Warnings.Add($"User {user.Id} skipped: no mail or userPrincipalName.");
                    continue;
                }
                if (name.Length > MaxNameLength)
                {
                    changes.Warnings.Add($"User {user.Id} skipped: name longer than {MaxNameLength} characters.");
                    continue;
                }
                if (!seenNames.Add(name))
                {
                    changes.Warnings.Add($"User {user.Id} skipped: duplicate name '{name}' in Entra.");
                    continue;
                }

                eligible.Add((user, name));
                eligibleIds.Add(user.Id);
            }

            stats.MatchedUsers = eligible.Count;

            if (eligible.Count == 0)
            {
                var wouldDisable = local.Identities.Count(o => o.Type == IdentityType.User && o.Source == IdentitySource.Entra && o.ExternalId != null && !IsDisabled(o, now));
                if (wouldDisable > 0)
                    throw new EntraSyncException($"Entra returned 0 matching users; refusing to disable {wouldDisable} identities. Check the filter/tenant.");
            }

            var resolvedUsers = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var (user, name) in eligible)
            {
                var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? name : user.DisplayName.Trim();
                if (displayName.Length > MaxNameLength) displayName = displayName[..MaxNameLength];

                LocalIdentity? existing = null;
                var changed = false;

                if (byExternalId.TryGetValue(user.Id, out var byExt))
                {
                    if (byExt.Type != IdentityType.User)
                    {
                        changes.Warnings.Add($"User '{name}' skipped: Entra id {user.Id} is linked to a non-user identity '{byExt.Name}'.");
                        continue;
                    }
                    existing = byExt;
                }
                else if (byName.TryGetValue(name, out var byNm))
                {
                    if (byNm.Type != IdentityType.User)
                    {
                        changes.Warnings.Add($"User '{name}' skipped: a local {byNm.Type.ToString().ToLowerInvariant()} already has this name.");
                        continue;
                    }
                    if (byNm.ExternalId != null && byNm.ExternalId != user.Id)
                    {
                        changes.Warnings.Add($"User '{name}' skipped: local identity is already linked to another Entra object.");
                        continue;
                    }
                    existing = byNm;
                    Update(byNm.Id).LinkExternalId = user.Id;
                    stats.UsersLinked++;
                    changed = true;
                }

                if (existing is null)
                {
                    if (usedNames.Contains(name))
                    {
                        changes.Warnings.Add($"User '{name}' skipped: name already used by another identity in this run.");
                        continue;
                    }
                    var id = newId();
                    changes.Creates.Add(new IdentityCreate(id, name, displayName, IdentityType.User, user.Id));
                    usedNames.Add(name);
                    resolvedUsers[user.Id] = id;
                    stats.UsersCreated++;
                    continue;
                }

                resolvedUsers[user.Id] = existing.Id;

                if (existing.Name != name)
                {
                    if (usedNames.Contains(name))
                    {
                        changes.Warnings.Add($"User '{existing.Name}' not renamed to '{name}': name already in use.");
                    }
                    else
                    {
                        Update(existing.Id).NewName = name;
                        // old name stays reserved this run: a chained/swapped rename would hit the unique index mid-batch
                        usedNames.Add(name);
                        changed = true;
                    }
                }

                if (existing.DisplayName != displayName)
                {
                    Update(existing.Id).NewDisplayName = displayName;
                    changed = true;
                }

                // Re-enable only identities this sync disabled (Source=Entra); a local identity's lock is never ours.
                if (existing.Source == IdentitySource.Entra && IsDisabled(existing, now))
                {
                    Update(existing.Id).Enable = true;
                    stats.UsersEnabled++;
                    changed = true;
                }

                if (changed) stats.UsersUpdated++;
            }

            // Users that were synced before but are no longer eligible (deleted/disabled in Entra, or filtered out).
            foreach (var absent in local.Identities.Where(o => o.Type == IdentityType.User && o.ExternalId != null && !candidateIds.Contains(o.ExternalId)))
            {
                if (absent.Source == IdentitySource.Entra && !IsDisabled(absent, now))
                {
                    Update(absent.Id).Disable = true;
                    stats.UsersDisabled++;
                }
                // Entra memberships of absent users are dropped by the membership diff below.
            }

            // ---- Groups ----------------------------------------------------------------------------------------
            var resolvedGroups = new Dictionary<string, long>(StringComparer.Ordinal);
            var syncedGroupIds = new HashSet<string>(StringComparer.Ordinal);
            var seenGroupNames = new HashSet<string>(StringComparer.Ordinal);
            var filterEnabled = filter is { Enabled: true } && filter.Rules.Count > 0;

            foreach (var group in entra.Groups.OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                var name = group.DisplayName?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
                {
                    changes.Warnings.Add($"Group {group.Id} skipped: missing or too long display name.");
                    continue;
                }
                if (name == UsersGroupName)
                    continue; // built-in group, never touched
                if (!seenGroupNames.Add(name))
                {
                    changes.Warnings.Add($"Group {group.Id} skipped: duplicate display name '{name}' in Entra.");
                    continue;
                }
                if (filterEnabled && !group.MemberUserIds.Any(eligibleIds.Contains))
                    continue;

                syncedGroupIds.Add(group.Id);
                stats.SyncedGroups++;
                var displayName = group.DisplayName!.Trim();

                LocalIdentity? existing = null;
                if (byExternalId.TryGetValue(group.Id, out var byExt))
                {
                    if (byExt.Type != IdentityType.Group)
                    {
                        changes.Warnings.Add($"Group '{name}' skipped: Entra id {group.Id} is linked to a non-group identity.");
                        continue;
                    }
                    existing = byExt;
                }
                else if (byName.TryGetValue(name, out var byNm))
                {
                    if (byNm.Type != IdentityType.Group)
                    {
                        changes.Warnings.Add($"Group '{name}' skipped: a local {byNm.Type.ToString().ToLowerInvariant()} already has this name.");
                        continue;
                    }
                    if (byNm.ExternalId != null && byNm.ExternalId != group.Id)
                    {
                        changes.Warnings.Add($"Group '{name}' skipped: local group is already linked to another Entra object.");
                        continue;
                    }
                    existing = byNm;
                    Update(byNm.Id).LinkExternalId = group.Id;
                    stats.GroupsLinked++;
                }

                if (existing is null)
                {
                    if (usedNames.Contains(name))
                    {
                        changes.Warnings.Add($"Group '{name}' skipped: name already used by another identity in this run.");
                        continue;
                    }
                    var id = newId();
                    changes.Creates.Add(new IdentityCreate(id, name, displayName, IdentityType.Group, group.Id));
                    usedNames.Add(name);
                    resolvedGroups[group.Id] = id;
                    stats.GroupsCreated++;
                    continue;
                }

                resolvedGroups[group.Id] = existing.Id;
                var changed = existing.ExternalId == null; // newly linked

                if (existing.Name != name)
                {
                    if (usedNames.Contains(name))
                    {
                        changes.Warnings.Add($"Group '{existing.Name}' not renamed to '{name}': name already in use.");
                    }
                    else
                    {
                        Update(existing.Id).NewName = name;
                        // old name stays reserved this run: a chained/swapped rename would hit the unique index mid-batch
                        usedNames.Add(name);
                        changed = true;
                    }
                }
                if (existing.DisplayName != displayName)
                {
                    Update(existing.Id).NewDisplayName = displayName;
                    changed = true;
                }
                if (changed) stats.GroupsUpdated++;
            }

            // Only groups that no longer exist in Entra are deleted (that cascades ACL grants). A group that is merely
            // filtered out or empty is kept; the membership diff below drops its Entra memberships.
            var entraGroupIds = entra.Groups.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
            var deletedGroupIds = new HashSet<long>();
            foreach (var orphan in local.Identities.Where(o => o.Type == IdentityType.Group && o.Source == IdentitySource.Entra && o.ExternalId != null && !entraGroupIds.Contains(o.ExternalId)))
            {
                changes.DeleteIds.Add(orphan.Id);
                deletedGroupIds.Add(orphan.Id);
                stats.GroupsDeleted++;
            }

            // ---- Memberships -----------------------------------------------------------------------------------
            var usersGroup = local.Identities.FirstOrDefault(o => o.Type == IdentityType.Group && o.Name == UsersGroupName);
            if (usersGroup is null)
                changes.Warnings.Add("Built-in 'users' group not found; new users were not added to it.");

            var desired = new HashSet<MembershipKey>();
            foreach (var (user, _) in eligible)
            {
                if (!resolvedUsers.TryGetValue(user.Id, out var userId)) continue;
                if (usersGroup is not null)
                    desired.Add(new MembershipKey(userId, usersGroup.Id));
            }
            foreach (var group in entra.Groups)
            {
                if (!resolvedGroups.TryGetValue(group.Id, out var groupId)) continue;
                foreach (var memberId in group.MemberUserIds)
                {
                    if (resolvedUsers.TryGetValue(memberId, out var userId))
                        desired.Add(new MembershipKey(userId, groupId));
                }
            }

            var existing2 = local.Memberships.ToDictionary(o => new MembershipKey(o.IdentityId, o.GroupId), o => o.Source);
            foreach (var key in desired.OrderBy(o => o.GroupId).ThenBy(o => o.IdentityId))
            {
                if (!existing2.ContainsKey(key)) // a Local row stays Local even when Entra also lists it
                    changes.MembershipAdds.Add(key);
            }
            foreach (var (key, source) in existing2)
            {
                if (source == IdentitySource.Entra && !desired.Contains(key) && !deletedGroupIds.Contains(key.GroupId))
                    changes.MembershipRemoves.Add(key);
            }
            stats.MembershipsAdded = changes.MembershipAdds.Count;
            stats.MembershipsRemoved = changes.MembershipRemoves.Count;

            changes.Updates.AddRange(updates.Values);
            return changes;
        }

        private static bool IsDisabled(LocalIdentity identity, DateTimeOffset now) => identity.LockAfter.HasValue && identity.LockAfter.Value <= now;
    }
}
