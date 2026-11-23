using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services.Entra;

namespace ChurrOS.Api.Tests.Services.Entra;

public class EntraSyncPlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private const long UsersGroupId = 1;

    private static LocalIdentity UsersGroup() => new(UsersGroupId, "users", "Users", IdentityType.Group, IdentitySource.Local, null, null);

    private static LocalIdentity LocalUser(long id, string name, IdentitySource source = IdentitySource.Local, string? externalId = null, DateTimeOffset? lockAfter = null) =>
        new(id, name, name, IdentityType.User, source, externalId, lockAfter);

    private static LocalIdentity LocalGroup(long id, string name, IdentitySource source = IdentitySource.Local, string? externalId = null) =>
        new(id, name, name, IdentityType.Group, source, externalId, null);

    private static EntraUser User(string id, string mail, params (string K, string? V)[] props) =>
        new(id, mail, mail, mail, true, new Dictionary<string, string?>(props.ToDictionary(o => o.K, o => o.V), StringComparer.OrdinalIgnoreCase));

    private static EntraGroup Group(string id, string name, params string[] members) => new(id, name, members.ToHashSet());

    private static SyncChanges Plan(LocalSnapshot local, EntraSnapshot entra, EntraFilter? filter = null)
    {
        long next = 1000;
        return EntraSyncPlanner.Plan(local, entra, filter, () => ++next, Now);
    }

    private static LocalSnapshot Local(IEnumerable<LocalIdentity> identities, params LocalMembership[] memberships) =>
        new(new[] { UsersGroup() }.Concat(identities).ToList(), memberships);

    [Fact]
    public void NewUser_IsCreatedAsEntraAndAddedToUsersGroup()
    {
        var changes = Plan(Local(Array.Empty<LocalIdentity>()), new EntraSnapshot(new[] { User("u1", "Ana@Epsa.com") }, Array.Empty<EntraGroup>()));

        var created = Assert.Single(changes.Creates);
        Assert.Equal("ana@epsa.com", created.Name);
        Assert.Equal(IdentityType.User, created.Type);
        Assert.Equal("u1", created.ExternalId);
        Assert.Contains(new MembershipKey(created.Id, UsersGroupId), changes.MembershipAdds);
    }

    [Fact]
    public void ExistingLocalUser_IsLinkedNotDisabled_AndKeepsRole()
    {
        var admin = LocalUser(10, "boss@epsa.com");
        var changes = Plan(Local(new[] { admin }), new EntraSnapshot(new[] { User("u1", "boss@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.Empty(changes.Creates);
        var update = Assert.Single(changes.Updates);
        Assert.Equal(10, update.Id);
        Assert.Equal("u1", update.LinkExternalId);
        Assert.False(update.Disable);
        Assert.Equal(1, changes.Stats.UsersLinked);
    }

    [Fact]
    public void LinkedLocalUser_MissingFromEntra_IsNeverDisabled()
    {
        var linked = LocalUser(10, "old@epsa.com", IdentitySource.Local, "gone");
        var changes = Plan(Local(new[] { linked }), new EntraSnapshot(new[] { User("u1", "other@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.DoesNotContain(changes.Updates, o => o.Disable);
    }

    [Fact]
    public void AbsentEntraUser_IsDisabled_AndItsEntraMembershipsRemoved_ButNotLocalOnes()
    {
        var gone = LocalUser(10, "ana@epsa.com", IdentitySource.Entra, "u-gone");
        var group = LocalGroup(20, "ops");
        var local = Local(new[] { gone, group },
            new LocalMembership(10, UsersGroupId, IdentitySource.Entra),
            new LocalMembership(10, 20, IdentitySource.Local));

        var changes = Plan(local, new EntraSnapshot(new[] { User("u1", "other@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.True(Assert.Single(changes.Updates, o => o.Id == 10).Disable);
        Assert.Contains(new MembershipKey(10, UsersGroupId), changes.MembershipRemoves);
        Assert.DoesNotContain(new MembershipKey(10, 20), changes.MembershipRemoves);
    }

    [Fact]
    public void UserDisabledInEntra_IsTreatedAsAbsent()
    {
        var existing = LocalUser(10, "ana@epsa.com", IdentitySource.Entra, "u1");
        var entraUser = User("u1", "ana@epsa.com") with { AccountEnabled = false };
        var changes = Plan(Local(new[] { existing }), new EntraSnapshot(new[] { entraUser, User("u2", "b@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.True(Assert.Single(changes.Updates, o => o.Id == 10).Disable);
    }

    [Fact]
    public void ReturningEntraUser_IsReEnabled()
    {
        var locked = LocalUser(10, "ana@epsa.com", IdentitySource.Entra, "u1", Now.AddDays(-1));
        var changes = Plan(Local(new[] { locked }), new EntraSnapshot(new[] { User("u1", "ana@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.True(Assert.Single(changes.Updates, o => o.Id == 10).Enable);
    }

    [Fact]
    public void ZeroMatches_WithExistingEntraUsers_FailsAndDisablesNothing()
    {
        var existing = LocalUser(10, "ana@epsa.com", IdentitySource.Entra, "u1");
        var filter = new EntraFilter { Enabled = true, Rules = { new EntraFilterRule { Property = "companyName", Values = new[] { "Nope" } } } };

        var ex = Assert.Throws<EntraSyncException>(() =>
            Plan(Local(new[] { existing }), new EntraSnapshot(new[] { User("u1", "ana@epsa.com", ("companyName", "EPSA")) }, Array.Empty<EntraGroup>()), filter));
        Assert.Contains("refusing to disable 1", ex.Message);
    }

    [Fact]
    public void Filter_ExcludesUsersFromCreation()
    {
        var filter = new EntraFilter { Enabled = true, Rules = { new EntraFilterRule { Property = "companyName", Values = new[] { "epsa" } } } };
        var changes = Plan(Local(Array.Empty<LocalIdentity>()),
            new EntraSnapshot(new[] { User("u1", "a@epsa.com", ("companyName", "EPSA")), User("u2", "b@x.com", ("companyName", "X")) }, Array.Empty<EntraGroup>()), filter);

        Assert.Equal("a@epsa.com", Assert.Single(changes.Creates).Name);
    }

    [Fact]
    public void Group_WithMatchingMember_IsCreatedWithOnlyMatchingMembers()
    {
        var filter = new EntraFilter { Enabled = true, Rules = { new EntraFilterRule { Property = "companyName", Values = new[] { "epsa" } } } };
        var changes = Plan(Local(Array.Empty<LocalIdentity>()),
            new EntraSnapshot(
                new[] { User("u1", "a@epsa.com", ("companyName", "EPSA")), User("u2", "b@x.com", ("companyName", "X")) },
                new[] { Group("g1", "Finance", "u1", "u2"), Group("g2", "Other", "u2") }), filter);

        var group = Assert.Single(changes.Creates, o => o.Type == IdentityType.Group);
        Assert.Equal("finance", group.Name);
        var userId = Assert.Single(changes.Creates, o => o.Type == IdentityType.User).Id;
        Assert.Contains(new MembershipKey(userId, group.Id), changes.MembershipAdds);
        Assert.Equal(2, changes.MembershipAdds.Count); // users + finance, no membership for the filtered-out user
    }

    [Fact]
    public void OrphanedEntraGroup_IsDeleted_ButLocalLinkedGroupIsKept()
    {
        var entraGroup = LocalGroup(20, "finance", IdentitySource.Entra, "g-old");
        var linked = LocalGroup(21, "ops", IdentitySource.Local, "g-ops-old");
        var changes = Plan(Local(new[] { entraGroup, linked }), new EntraSnapshot(new[] { User("u1", "a@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.Equal(new long[] { 20 }, changes.DeleteIds);
    }

    [Fact]
    public void Membership_LocalRowStaysLocal_StaleEntraRowRemoved()
    {
        var user = LocalUser(10, "a@epsa.com", IdentitySource.Entra, "u1");
        var ops = LocalGroup(20, "ops", IdentitySource.Entra, "g1");
        var old = LocalGroup(21, "old", IdentitySource.Local);
        var local = Local(new[] { user, ops, old },
            new LocalMembership(10, UsersGroupId, IdentitySource.Local), // admin-added, Entra also wants it
            new LocalMembership(10, 20, IdentitySource.Entra),
            new LocalMembership(10, 21, IdentitySource.Entra)); // stale Entra row

        var changes = Plan(local, new EntraSnapshot(new[] { User("u1", "a@epsa.com") }, new[] { Group("g1", "ops", "u1") }));

        Assert.Empty(changes.MembershipAdds);
        Assert.Equal(new[] { new MembershipKey(10, 21) }, changes.MembershipRemoves);
    }

    [Fact]
    public void DuplicateGroupNames_AreSkippedWithWarning()
    {
        var changes = Plan(Local(Array.Empty<LocalIdentity>()),
            new EntraSnapshot(new[] { User("u1", "a@epsa.com") }, new[] { Group("g1", "Dup", "u1"), Group("g2", "dup", "u1") }));

        Assert.Single(changes.Creates, o => o.Type == IdentityType.Group);
        Assert.Contains(changes.Warnings, o => o.Contains("duplicate"));
    }

    [Fact]
    public void BuiltInUsersGroup_IsNeverTouched()
    {
        var changes = Plan(Local(Array.Empty<LocalIdentity>()),
            new EntraSnapshot(new[] { User("u1", "a@epsa.com") }, new[] { Group("g1", "Users", "u1") }));

        Assert.DoesNotContain(changes.Creates, o => o.Type == IdentityType.Group);
        Assert.DoesNotContain(changes.Updates, o => o.Id == UsersGroupId);
    }

    [Fact]
    public void NameCollidingWithLocalGroup_IsSkippedWithWarning()
    {
        var group = LocalGroup(20, "a@epsa.com");
        var changes = Plan(Local(new[] { group }), new EntraSnapshot(new[] { User("u1", "a@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.Empty(changes.Creates);
        Assert.Single(changes.Warnings);
    }

    [Fact]
    public void GroupFilteredOut_IsKept_ButItsEntraMembershipsAreDropped()
    {
        var filter = new EntraFilter { Enabled = true, Rules = { new EntraFilterRule { Property = "companyName", Values = new[] { "epsa" } } } };
        var user = LocalUser(10, "a@epsa.com", IdentitySource.Entra, "u1");
        var finance = LocalGroup(20, "finance", IdentitySource.Entra, "g1");
        var local = Local(new[] { user, finance }, new LocalMembership(10, 20, IdentitySource.Entra));

        var changes = Plan(local, new EntraSnapshot(
            new[] { User("u1", "a@epsa.com", ("companyName", "EPSA")), User("u2", "b@x.com", ("companyName", "X")) },
            new[] { Group("g1", "Finance", "u2") }), filter);

        Assert.Empty(changes.DeleteIds);
        Assert.Contains(new MembershipKey(10, 20), changes.MembershipRemoves);
    }

    [Fact]
    public void NewGroupAndNewUserWithSameName_OnlyTheFirstIsCreated()
    {
        var changes = Plan(Local(Array.Empty<LocalIdentity>()),
            new EntraSnapshot(new[] { User("u1", "ana@epsa.com") }, new[] { Group("g1", "Ana@Epsa.com", "u1") }));

        Assert.Single(changes.Creates);
        Assert.Contains(changes.Warnings, o => o.Contains("already used"));
    }

    [Fact]
    public void UserSkippedByWarning_IsNotDisabledAsAbsent()
    {
        var existing = LocalUser(10, "ana@epsa.com", IdentitySource.Entra, "u1");
        var noMail = new EntraUser("u1", "Ana", null, null, true, new Dictionary<string, string?>());
        var changes = Plan(Local(new[] { existing }), new EntraSnapshot(new[] { noMail, User("u2", "b@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.DoesNotContain(changes.Updates, o => o.Id == 10 && o.Disable);
    }

    [Fact]
    public void ChainedRename_IsDeferredToTheNextRun()
    {
        var u1 = LocalUser(10, "x@epsa.com", IdentitySource.Entra, "u1");
        var u2 = LocalUser(11, "y@epsa.com", IdentitySource.Entra, "u2");
        // Entra reassigns x@ to u2 and gives u1 a new address in the same directory change.
        var changes = Plan(Local(new[] { u1, u2 }), new EntraSnapshot(new[] { User("u1", "z@epsa.com"), User("u2", "x@epsa.com") }, Array.Empty<EntraGroup>()));

        Assert.Equal("z@epsa.com", Assert.Single(changes.Updates, o => o.Id == 10).NewName);
        Assert.DoesNotContain(changes.Updates, o => o.Id == 11 && o.NewName != null);
        Assert.Contains(changes.Warnings, o => o.Contains("not renamed"));
    }
}
