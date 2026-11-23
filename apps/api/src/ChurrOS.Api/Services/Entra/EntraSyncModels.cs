using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Services.Entra
{
    /// <summary>A user as returned by Microsoft Graph, reduced to what the sync needs.</summary>
    /// <param name="Properties">Scalar string values of every selected property (case-insensitive keys); used by the filter.</param>
    public record EntraUser(string Id, string? DisplayName, string? Mail, string? UserPrincipalName, bool AccountEnabled, IReadOnlyDictionary<string, string?> Properties);

    public record EntraGroup(string Id, string? DisplayName, IReadOnlySet<string> MemberUserIds);

    public record EntraSnapshot(IReadOnlyList<EntraUser> Users, IReadOnlyList<EntraGroup> Groups);

    public record LocalIdentity(long Id, string Name, string DisplayName, IdentityType Type, IdentitySource Source, string? ExternalId, DateTimeOffset? LockAfter);

    public record LocalMembership(long IdentityId, long GroupId, IdentitySource Source);

    public record LocalSnapshot(IReadOnlyList<LocalIdentity> Identities, IReadOnlyList<LocalMembership> Memberships);

    public record IdentityCreate(long Id, string Name, string DisplayName, IdentityType Type, string ExternalId);

    public class IdentityUpdate
    {
        public IdentityUpdate(long id) { Id = id; }
        public long Id { get; }
        public string? NewName { get; set; }
        public string? NewDisplayName { get; set; }
        public string? LinkExternalId { get; set; }
        public bool Enable { get; set; }
        public bool Disable { get; set; }
    }

    public record MembershipKey(long IdentityId, long GroupId);

    public class SyncStats
    {
        public int UsersCreated { get; set; }
        public int UsersUpdated { get; set; }
        public int UsersLinked { get; set; }
        public int UsersDisabled { get; set; }
        public int UsersEnabled { get; set; }
        public int GroupsCreated { get; set; }
        public int GroupsUpdated { get; set; }
        public int GroupsLinked { get; set; }
        public int GroupsDeleted { get; set; }
        public int MembershipsAdded { get; set; }
        public int MembershipsRemoved { get; set; }
        public int MatchedUsers { get; set; }
        public int SyncedGroups { get; set; }

        public string ToSummary() =>
            $"{UsersCreated} created, {UsersUpdated} updated ({UsersLinked} linked), {UsersDisabled} disabled, {UsersEnabled} re-enabled; " +
            $"groups: {GroupsCreated} created, {GroupsUpdated} updated, {GroupsDeleted} deleted; " +
            $"memberships: {MembershipsAdded} added, {MembershipsRemoved} removed";
    }

    public class SyncChanges
    {
        public List<IdentityCreate> Creates { get; } = new();
        public List<IdentityUpdate> Updates { get; } = new();
        public List<long> DeleteIds { get; } = new();
        public List<MembershipKey> MembershipAdds { get; } = new();
        public List<MembershipKey> MembershipRemoves { get; } = new();
        public List<string> Warnings { get; } = new();
        public SyncStats Stats { get; } = new();
    }

    public class EntraSyncException : Exception
    {
        public EntraSyncException(string message) : base(message) { }
        public EntraSyncException(string message, Exception inner) : base(message, inner) { }
    }
}
