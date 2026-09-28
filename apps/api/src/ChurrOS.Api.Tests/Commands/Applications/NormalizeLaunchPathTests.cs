using ChurrOS.Api.Commands.Applications;

namespace ChurrOS.Api.Tests.Commands.Applications;

public class NormalizeLaunchPathTests
{
    [Fact]
    public void NullRequest_KeepsCurrentValue()
    {
        var result = UpdateApplicationHandler.NormalizeLaunchPath(null, "/current");

        Assert.Equal("/current", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceRequest_ClearsTheValue(string requested)
    {
        var result = UpdateApplicationHandler.NormalizeLaunchPath(requested, "/current");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/a/b/c?q=v&x=6")]
    [InlineData("?x=1")]
    [InlineData("#anchor")]
    public void ValidRequest_IsKeptVerbatim(string requested)
    {
        var result = UpdateApplicationHandler.NormalizeLaunchPath(requested, null);

        Assert.Equal(requested, result);
    }

    [Fact]
    public void ValueIsTrimmedBeforeValidationAndStorage()
    {
        var result = UpdateApplicationHandler.NormalizeLaunchPath("  /a/b  ", null);

        Assert.Equal("/a/b", result);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("http://example.com")]
    public void ValueNotStartingWithSlashQuestionOrHash_Throws(string requested)
    {
        Assert.Throws<ArgumentException>(() => UpdateApplicationHandler.NormalizeLaunchPath(requested, null));
    }

    [Fact]
    public void ValueLongerThan2048Characters_Throws()
    {
        var requested = "/" + new string('a', 2048);

        Assert.Throws<ArgumentException>(() => UpdateApplicationHandler.NormalizeLaunchPath(requested, null));
    }
}
