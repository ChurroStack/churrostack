using NSubstitute;
using OpenIddict.Abstractions;

namespace ChurrOS.Api.Tests;

public class RedirectUriValidationTests
{
    // IsSameOriginRedirectUri no longer compares scheme against the caller's requestScheme: the
    // app forces Request.Scheme to "https" unconditionally before OpenIddict's pipeline runs
    // (Program.cs), so a requestScheme parameter can never actually be "http" at runtime. Instead
    // the redirect_uri's own scheme is checked: https required, unless the host is loopback.
    [Theory]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/callback", true)]
    [InlineData("localhost", "http://localhost:5173/callback", true)]
    [InlineData("portal.churrostack.com", "http://portal.churrostack.com/callback", false)]
    [InlineData("portal.churrostack.com", "https://evil.com/callback", false)]
    public void IsSameOriginRedirectUri_RequiresMatchingHostAndHttpsUnlessLoopback(
        string requestHost, string redirectUri, bool expected)
    {
        var result = Program.IsSameOriginRedirectUri(new Uri(redirectUri), requestHost);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/login/signin-oidc", true)]
    [InlineData("other.churrostack.com", "https://other.churrostack.com/login/signin-oidc", true)]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/some/other/path", false)]
    [InlineData("portal.churrostack.com", "https://evil.com/login/signin-oidc", false)]
    public void IsApiRedirectUri_AcceptsAnyHost_ButOnlyTheFixedCallbackPath(
        string requestHost, string redirectUri, bool expected)
    {
        var result = Program.IsApiRedirectUri(new Uri(redirectUri), requestHost);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/", true)]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/share/some-app/8080/", false)]
    [InlineData("portal.churrostack.com", "https://portal.churrostack.com/oauth/authorize", false)]
    [InlineData("portal.churrostack.com", "https://evil.com/", false)]
    public void IsAppRedirectUri_RequiresRootPath_RejectingTenantControlledSameOriginPaths(
        string requestHost, string redirectUri, bool expected)
    {
        var result = Program.IsAppRedirectUri(new Uri(redirectUri), requestHost);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IsRegisteredLoopbackAwareRedirectUriAsync_AcceptsAnyPort_WhenRegisteredHostIsLoopback()
    {
        var applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        var application = new object();
        applicationManager.FindByClientIdAsync("mcp-client").Returns(application);
        applicationManager.GetRedirectUrisAsync(application)
            .Returns(["http://127.0.0.1/callback"]);

        var result = await Program.IsRegisteredLoopbackAwareRedirectUriAsync(
            applicationManager, "mcp-client", new Uri("http://127.0.0.1:54321/callback"));

        Assert.True(result);
    }

    [Fact]
    public async Task IsRegisteredLoopbackAwareRedirectUriAsync_AcceptsAnyPort_WhenRegisteredHostIsIPv6Loopback()
    {
        // Uri.Host renders an IPv6 literal in bracket notation ("[::1]"), which must still match
        // registration against the bracket-free "::1" -- see Program.IsLoopbackHost.
        var applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        var application = new object();
        applicationManager.FindByClientIdAsync("mcp-client").Returns(application);
        applicationManager.GetRedirectUrisAsync(application)
            .Returns(["http://[::1]:1234/callback"]);

        var result = await Program.IsRegisteredLoopbackAwareRedirectUriAsync(
            applicationManager, "mcp-client", new Uri("http://[::1]:54321/callback"));

        Assert.True(result);
    }

    [Fact]
    public async Task IsRegisteredLoopbackAwareRedirectUriAsync_RejectsDifferentPort_WhenRegisteredHostIsNotLoopback()
    {
        var applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        var application = new object();
        applicationManager.FindByClientIdAsync("web-client").Returns(application);
        applicationManager.GetRedirectUrisAsync(application)
            .Returns(["https://example.com:8443/callback"]);

        var result = await Program.IsRegisteredLoopbackAwareRedirectUriAsync(
            applicationManager, "web-client", new Uri("https://example.com:9999/callback"));

        Assert.False(result);
    }

    [Fact]
    public async Task IsRegisteredLoopbackAwareRedirectUriAsync_RejectsUnregisteredHost_EvenWhenIncomingHostIsLoopback()
    {
        var applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        var application = new object();
        applicationManager.FindByClientIdAsync("web-client").Returns(application);
        applicationManager.GetRedirectUrisAsync(application)
            .Returns(["https://example.com/callback"]);

        var result = await Program.IsRegisteredLoopbackAwareRedirectUriAsync(
            applicationManager, "web-client", new Uri("http://127.0.0.1:54321/callback"));

        Assert.False(result);
    }

    [Fact]
    public async Task IsRegisteredLoopbackAwareRedirectUriAsync_RejectsUnknownClient()
    {
        var applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        applicationManager.FindByClientIdAsync("unknown").Returns((object?)null);

        var result = await Program.IsRegisteredLoopbackAwareRedirectUriAsync(
            applicationManager, "unknown", new Uri("http://127.0.0.1/callback"));

        Assert.False(result);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("[::1]", true)]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("example.com", false)]
    public void IsLoopbackHost_NormalizesIPv6BracketNotation(string host, bool expected)
    {
        Assert.Equal(expected, Program.IsLoopbackHost(host));
    }
}
