using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Services;
using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace ChurrOS.Api.Tests.Data;

public class ChurrosDbContextLlmTenantFilterTests
{
    private static Llm CreateLlm(long accountId, long id) => new(
        accountId,
        id,
        [$"model-{id}"],
        aclId: 1,
        routing: LLmRoutingType.RoundRobin,
        destination: [new LLmDestinationItem(LLmDestinationType.OpenAI, "https://example.com", "gpt", "secret-api-key", null, null)],
        fallback: null,
        createdAt: DateTimeOffset.UtcNow,
        createdById: 1,
        modifiedAt: DateTimeOffset.UtcNow,
        modifiedById: 1,
        capabilities: new Dictionary<string, bool>());

    private static ChurrosDbContext CreateContext(string databaseName, long accountId)
    {
        var tenantResolver = Substitute.For<ITenantResolver>();
        tenantResolver.AccountId.Returns(accountId);
        var options = new DbContextOptionsBuilder<ChurrosDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new ChurrosDbContext(
            options,
            tenantResolver,
            Substitute.For<IAppCache>(),
            new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task Set_Llm_IsScopedToTheCurrentAccount_EvenWithoutAnExplicitWhereClause()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var seedContext = CreateContext(databaseName, accountId: 1))
        {
            seedContext.Set<Llm>().Add(CreateLlm(accountId: 1, id: 100));
            seedContext.Set<Llm>().Add(CreateLlm(accountId: 2, id: 200));
            await seedContext.SaveChangesAsync();
        }

        await using var accountOneContext = CreateContext(databaseName, accountId: 1);
        var accountOneLlms = await accountOneContext.Set<Llm>().AsNoTracking().ToListAsync();

        await using var accountTwoContext = CreateContext(databaseName, accountId: 2);
        var accountTwoLlms = await accountTwoContext.Set<Llm>().AsNoTracking().ToListAsync();

        Assert.Single(accountOneLlms);
        Assert.Equal(100, accountOneLlms[0].Id);
        Assert.Single(accountTwoLlms);
        Assert.Equal(200, accountTwoLlms[0].Id);
    }
}
