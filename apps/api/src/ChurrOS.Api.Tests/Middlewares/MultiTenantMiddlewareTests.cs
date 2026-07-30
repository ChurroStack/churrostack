using System.Security.Claims;
using ChurrOS.Api.Middlewares;
using ChurrOS.Api.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace ChurrOS.Api.Tests.Middlewares;

public class MultiTenantMiddlewareTests
{
    [Fact]
    public async Task Invoke_ResolvesEachRequestedTenantIndependently()
    {
        var resolver = Substitute.For<IAccountMembershipResolver>();
        resolver.ResolveAccountIdAsync("alice", 101, Arg.Any<CancellationToken>()).Returns(101);
        resolver.ResolveAccountIdAsync("alice", 202, Arg.Any<CancellationToken>()).Returns(202);
        var nextCalls = 0;
        var middleware = new MultiTenantMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        var firstContext = CreateAuthenticatedContext("alice", 101);
        await middleware.Invoke(firstContext, resolver);
        var secondContext = CreateAuthenticatedContext("alice", 202);
        await middleware.Invoke(secondContext, resolver);

        Assert.Equal(101L, firstContext.Items["AccountId"]);
        Assert.Equal(202L, secondContext.Items["AccountId"]);
        Assert.Equal(2, nextCalls);
        await resolver.Received(1).ResolveAccountIdAsync("alice", 101, Arg.Any<CancellationToken>());
        await resolver.Received(1).ResolveAccountIdAsync("alice", 202, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoke_RechecksMembershipAndRejectsARevokedTenant()
    {
        var resolver = Substitute.For<IAccountMembershipResolver>();
        resolver.ResolveAccountIdAsync("alice", 101, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<long?>(101), Task.FromResult<long?>(null));
        var nextCalls = 0;
        var middleware = new MultiTenantMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        await middleware.Invoke(CreateAuthenticatedContext("alice", 101), resolver);
        var revokedContext = CreateAuthenticatedContext("alice", 101);
        await middleware.Invoke(revokedContext, resolver);

        Assert.Equal(1, nextCalls);
        Assert.Equal(StatusCodes.Status403Forbidden, revokedContext.Response.StatusCode);
        await resolver.Received(2).ResolveAccountIdAsync("alice", 101, Arg.Any<CancellationToken>());
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string identityName, long accountId)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, identityName)], "test"))
        };
        context.Request.Headers["X-TENANT-ID"] = accountId.ToString();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
