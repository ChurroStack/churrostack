using ChurrOS.Api.Controllers;

namespace ChurrOS.Api.Tests.Controllers;

public class OAuthControllerDcrTests
{
    [Theory]
    [InlineData("http://127.0.0.1:7777/cb", true)]
    [InlineData("http://[::1]:7777/cb", true)]
    [InlineData("http://localhost:7777/cb", true)]
    [InlineData("https://claude.ai/api/mcp/auth_callback", true)]
    [InlineData("https://evil.example/cb", true)]
    [InlineData("http://evil.example/cb", false)]
    [InlineData("https://claude.ai/cb#fragment", false)]
    [InlineData("not-a-uri", false)]
    [InlineData("ftp://127.0.0.1/cb", false)]
    public void ValidateDcrRedirectUri_AcceptsAnyHttps_AndHttpOnlyForLoopback(string redirectUri, bool expectedValid)
    {
        var (isValid, _) = OAuthController.ValidateDcrRedirectUri(redirectUri);

        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void IsValidDcrClientName_AcceptsNullOrShortPlainName()
    {
        Assert.True(OAuthController.IsValidDcrClientName(null));
        Assert.True(OAuthController.IsValidDcrClientName(""));
        Assert.True(OAuthController.IsValidDcrClientName("Claude"));
    }

    [Fact]
    public void IsValidDcrClientName_RejectsOverlyLongName()
    {
        var name = new string('a', 201);

        Assert.False(OAuthController.IsValidDcrClientName(name));
    }

    [Fact]
    public void IsValidDcrClientName_RejectsControlCharacters()
    {
        Assert.False(OAuthController.IsValidDcrClientName("Claude\u0007Bell"));
        Assert.False(OAuthController.IsValidDcrClientName("Claude\nNewline"));
    }
}
