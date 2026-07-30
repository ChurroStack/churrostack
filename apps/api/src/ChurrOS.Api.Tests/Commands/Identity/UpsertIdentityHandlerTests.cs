using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Services;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ChurrOS.Api.Tests.Commands.Identity;

public class UpsertIdentityHandlerTests
{
    [Fact]
    public async Task UpdateMembershipAsync_DefersTheCompleteSetDiffUntilSaveChanges()
    {
        const long accountId = 1;
        const long groupId = 100;
        var options = new DbContextOptionsBuilder<ChurrosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantResolver = Substitute.For<ITenantResolver>();
        tenantResolver.AccountId.Returns(accountId);

        await using (var seedContext = CreateContext(options, tenantResolver))
        {
            seedContext.Set<IdentityMemberOf>().AddRange(
                new IdentityMemberOf(accountId, 10, groupId),
                new IdentityMemberOf(accountId, 11, groupId));
            await seedContext.SaveChangesAsync();
        }

        await using var updateContext = CreateContext(options, tenantResolver);
        var handler = new UpsertIdentityHandler(
            updateContext,
            tenantResolver,
            null!,
            null!,
            null!,
            NullLogger<UpsertIdentityHandler>.Instance);

        var delta = await handler.UpdateMembershipAsync(groupId, [11, 12], true, CancellationToken.None);
        var persistedBeforeSave = await ReadGroupMemberIdsAsync(options, tenantResolver, groupId);

        Assert.Equal(new long[] { 10 }, delta.RemovedIds);
        Assert.Equal(new long[] { 12 }, delta.AddedIds);
        Assert.Equal(new long[] { 10, 11 }, persistedBeforeSave);

        await updateContext.SaveChangesAsync();
        var persistedAfterSave = await ReadGroupMemberIdsAsync(options, tenantResolver, groupId);

        Assert.Equal(new long[] { 11, 12 }, persistedAfterSave);
    }

    private static ChurrosDbContext CreateContext(DbContextOptions<ChurrosDbContext> options, ITenantResolver tenantResolver)
    {
        return new ChurrosDbContext(
            options,
            tenantResolver,
            Substitute.For<IAppCache>(),
            new ConfigurationBuilder().Build());
    }

    private static async Task<long[]> ReadGroupMemberIdsAsync(DbContextOptions<ChurrosDbContext> options, ITenantResolver tenantResolver, long groupId)
    {
        await using var context = CreateContext(options, tenantResolver);
        return await context.Set<IdentityMemberOf>()
            .Where(o => o.GroupId == groupId)
            .OrderBy(o => o.IdentityId)
            .Select(o => o.IdentityId)
            .ToArrayAsync();
    }
}
