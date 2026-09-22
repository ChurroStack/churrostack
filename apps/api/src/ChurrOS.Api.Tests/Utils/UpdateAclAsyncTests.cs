using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ChurrOS.Api.Tests.Utils;

public class UpdateAclAsyncTests
{
    private const long AccountId = 1;
    private const long AclId = 50;

    [Fact]
    public async Task EmptyMemberItems_RemovesEveryExistingMember()
    {
        var options = CreateOptions();
        var tenantResolver = CreateTenantResolver();

        await using (var seedContext = CreateContext(options, tenantResolver))
        {
            seedContext.Set<Identity>().Add(NewIdentity(10, "alice"));
            seedContext.Set<Identity>().Add(NewIdentity(11, "bob"));
            await seedContext.SaveChangesAsync();
            seedContext.Set<AclMember>().Add(new AclMember(AccountId, AclId, 10, Permission.Read));
            seedContext.Set<AclMember>().Add(new AclMember(AccountId, AclId, 11, Permission.Write));
            await seedContext.SaveChangesAsync();
        }

        await using var updateContext = CreateContext(options, tenantResolver);
        var mediator = Substitute.For<IMediator>();

        var updated = await mediator.UpdateAclAsync([], updateContext, AccountId, AclId, [], NullLogger.Instance, CancellationToken.None);
        await updateContext.SaveChangesAsync();

        Assert.Empty(updated);
        Assert.Empty(await ReadAclMembersAsync(options, tenantResolver));
    }

    [Fact]
    public async Task CaseDifferingIdentityName_UpdatesExistingMemberInsteadOfReplacingIt()
    {
        var options = CreateOptions();
        var tenantResolver = CreateTenantResolver();

        await using (var seedContext = CreateContext(options, tenantResolver))
        {
            seedContext.Set<Identity>().Add(NewIdentity(10, "alice"));
            await seedContext.SaveChangesAsync();
            seedContext.Set<AclMember>().Add(new AclMember(AccountId, AclId, 10, Permission.Read));
            await seedContext.SaveChangesAsync();
        }

        await using var updateContext = CreateContext(options, tenantResolver);
        var mediator = Substitute.For<IMediator>();
        // A casing match must resolve through the existing-member dictionary and never fall
        // through to identity resolution, which the earlier case-sensitive lookup used to do.
        mediator.Send(Arg.Any<GetIdentityId>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<long>>(_ => throw new InvalidOperationException("GetIdentityId must not be called for a member that already exists under a different case."));

        var updated = await mediator.UpdateAclAsync([], updateContext, AccountId, AclId, [new MemberItem("Alice", Permission.Write)], NullLogger.Instance, CancellationToken.None);
        await updateContext.SaveChangesAsync();

        Assert.Equal([10L], updated);
        var members = await ReadAclMembersAsync(options, tenantResolver);
        var member = Assert.Single(members);
        Assert.Equal(10, member.IdentityId);
        Assert.Equal(Permission.Write, member.Permission);
    }

    [Fact]
    public async Task UnresolvableIdentity_ThrowsAndLeavesExistingMembersUntouched()
    {
        var options = CreateOptions();
        var tenantResolver = CreateTenantResolver();

        await using (var seedContext = CreateContext(options, tenantResolver))
        {
            seedContext.Set<Identity>().Add(NewIdentity(10, "alice"));
            await seedContext.SaveChangesAsync();
            seedContext.Set<AclMember>().Add(new AclMember(AccountId, AclId, 10, Permission.Read));
            await seedContext.SaveChangesAsync();
        }

        await using var updateContext = CreateContext(options, tenantResolver);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GetIdentityId>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(0L));

        var memberItems = new[] { new MemberItem("alice", Permission.Write), new MemberItem("unknown-user", Permission.Read) };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            mediator.UpdateAclAsync([], updateContext, AccountId, AclId, memberItems, NullLogger.Instance, CancellationToken.None));

        // The caller's own SaveChangesAsync (which would apply staged removals/additions) never
        // runs after this throw, so a fresh context must still see the original, untouched row.
        var members = await ReadAclMembersAsync(options, tenantResolver);
        var member = Assert.Single(members);
        Assert.Equal(10, member.IdentityId);
        Assert.Equal(Permission.Read, member.Permission);
    }

    [Fact]
    public async Task PermissionChangeOnExistingMember_Persists()
    {
        var options = CreateOptions();
        var tenantResolver = CreateTenantResolver();

        await using (var seedContext = CreateContext(options, tenantResolver))
        {
            seedContext.Set<Identity>().Add(NewIdentity(10, "alice"));
            await seedContext.SaveChangesAsync();
            seedContext.Set<AclMember>().Add(new AclMember(AccountId, AclId, 10, Permission.Read));
            await seedContext.SaveChangesAsync();
        }

        await using var updateContext = CreateContext(options, tenantResolver);
        var mediator = Substitute.For<IMediator>();

        var updated = await mediator.UpdateAclAsync([], updateContext, AccountId, AclId, [new MemberItem("alice", Permission.Manage)], NullLogger.Instance, CancellationToken.None);
        await updateContext.SaveChangesAsync();

        Assert.Equal([10L], updated);
        var members = await ReadAclMembersAsync(options, tenantResolver);
        var member = Assert.Single(members);
        Assert.Equal(Permission.Manage, member.Permission);
    }

    private static DbContextOptions<ChurrosDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<ChurrosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static ITenantResolver CreateTenantResolver()
    {
        var tenantResolver = Substitute.For<ITenantResolver>();
        tenantResolver.AccountId.Returns(AccountId);
        return tenantResolver;
    }

    private static ChurrosDbContext CreateContext(DbContextOptions<ChurrosDbContext> options, ITenantResolver tenantResolver) =>
        new(options, tenantResolver, Substitute.For<IAppCache>(), new ConfigurationBuilder().Build());

    private static Identity NewIdentity(long id, string name) =>
        new(AccountId, id, name, name, IdentityType.User, IdentityRole.User, DateTimeOffset.Now, null, DateTimeOffset.Now, null);

    private static async Task<List<AclMember>> ReadAclMembersAsync(DbContextOptions<ChurrosDbContext> options, ITenantResolver tenantResolver)
    {
        await using var context = CreateContext(options, tenantResolver);
        return await context.Set<AclMember>()
            .Where(o => o.AclId == AclId)
            .OrderBy(o => o.IdentityId)
            .ToListAsync();
    }
}
