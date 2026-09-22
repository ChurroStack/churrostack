using System.Security.Claims;
using ChurrOS.Api.Middlewares;
using ChurrOS.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
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
        await middleware.Invoke(firstContext, resolver, NullLogger<MultiTenantMiddleware>.Instance);
        var secondContext = CreateAuthenticatedContext("alice", 202);
        await middleware.Invoke(secondContext, resolver, NullLogger<MultiTenantMiddleware>.Instance);

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

        await middleware.Invoke(CreateAuthenticatedContext("alice", 101), resolver, NullLogger<MultiTenantMiddleware>.Instance);
        var revokedContext = CreateAuthenticatedContext("alice", 101);
        await middleware.Invoke(revokedContext, resolver, NullLogger<MultiTenantMiddleware>.Instance);

        Assert.Equal(1, nextCalls);
        Assert.Equal(StatusCodes.Status403Forbidden, revokedContext.Response.StatusCode);
        await resolver.Received(2).ResolveAccountIdAsync("alice", 101, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoke_FallsBackToTidClaim_WhenNoTenantHeaderIsPresent()
    {
        // MCP clients (and any other pure bearer-token caller) have no way to send X-TENANT-ID --
        // the token's own "tid" claim (set by OAuthController.Exchange at issuance) is what a
        // multi-account identity's request should resolve against instead of the header-absent
        // "pick the only account" default, which 403s for anyone in more than one account.
        var resolver = Substitute.For<IAccountMembershipResolver>();
        resolver.ResolveAccountIdAsync("bob", 303, Arg.Any<CancellationToken>()).Returns(303);
        var nextCalls = 0;
        var middleware = new MultiTenantMiddleware(_ =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });

        var context = CreateAuthenticatedContextWithTidClaim("bob", tid: 303);
        await middleware.Invoke(context, resolver, NullLogger<MultiTenantMiddleware>.Instance);

        Assert.Equal(303L, context.Items["AccountId"]);
        Assert.Equal(1, nextCalls);
        await resolver.Received(1).ResolveAccountIdAsync("bob", 303, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invoke_PrefersTenantHeader_OverTidClaim_WhenBothArePresent()
    {
        var resolver = Substitute.For<IAccountMembershipResolver>();
        resolver.ResolveAccountIdAsync("bob", 404, Arg.Any<CancellationToken>()).Returns(404);
        var middleware = new MultiTenantMiddleware(_ => Task.CompletedTask);

        var context = CreateAuthenticatedContextWithTidClaim("bob", tid: 303);
        context.Request.Headers["X-TENANT-ID"] = "404";
        await middleware.Invoke(context, resolver, NullLogger<MultiTenantMiddleware>.Instance);

        Assert.Equal(404L, context.Items["AccountId"]);
        await resolver.DidNotReceive().ResolveAccountIdAsync("bob", 303, Arg.Any<CancellationToken>());
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

    private static DefaultHttpContext CreateAuthenticatedContextWithTidClaim(string identityName, long tid)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, identityName), new Claim("tid", tid.ToString())], "test"))
        };
        context.Response.Body = new MemoryStream();
        return context;
    }
}
