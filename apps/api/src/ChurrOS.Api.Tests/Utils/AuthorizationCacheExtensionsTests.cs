using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ChurrOS.Api.Tests.Utils;

public class AuthorizationCacheExtensionsTests
{
    [Fact]
    public async Task InvalidateIdentityAuthorizationCachesAsync_ExpandsGroupsToTheirMembers()
    {
        const long accountId = 1;
        const long groupId = 100;
        var tenantResolver = Substitute.For<ITenantResolver>();
        tenantResolver.AccountId.Returns(accountId);
        var options = new DbContextOptionsBuilder<ChurrosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ChurrosDbContext(
            options,
            tenantResolver,
            Substitute.For<IAppCache>(),
            new ConfigurationBuilder().Build());
        context.Set<IdentityMemberOf>().AddRange(
            new IdentityMemberOf(accountId, 10, groupId),
            new IdentityMemberOf(accountId, 11, groupId));
        await context.SaveChangesAsync();
        var cache = Substitute.For<ICacheService>();

        await cache.InvalidateIdentityAuthorizationCachesAsync(
            context,
            accountId,
            [groupId, 12],
            NullLogger.Instance,
            CancellationToken.None);

        await cache.Received(1).InvalidatePrefixAsync("tenant:1:identity:100");
        await cache.Received(1).InvalidatePrefixAsync("tenant:1:identity:12");
        await cache.Received(1).InvalidatePrefixAsync("tenant:1:identity:10");
        await cache.Received(1).InvalidatePrefixAsync("tenant:1:identity:11");
    }
}
