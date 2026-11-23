using ChurrOS.Api.Models.Dtos.IdentitySync;
using ChurrOS.Api.Services.Entra;

namespace ChurrOS.Api.Tests.Services.Entra;

public class EntraUserFilterTests
{
    private static IReadOnlyDictionary<string, string?> Props(params (string K, string? V)[] values) =>
        new Dictionary<string, string?>(values.ToDictionary(o => o.K, o => o.V), StringComparer.OrdinalIgnoreCase);

    private static EntraFilter Filter(params (string Property, string Values)[] rules) => new()
    {
        Enabled = true,
        Rules = rules.Select(o => new EntraFilterRule { Property = o.Property, Values = EntraUserFilter.ParseValues(o.Values) }).ToList()
    };

    [Fact]
    public void ParseValues_SplitsOnCommasAndTrims()
    {
        Assert.Equal(new[] { "A", "B", "C d" }, EntraUserFilter.ParseValues(" A, B ,,C d "));
        Assert.Empty(EntraUserFilter.ParseValues(null));
    }

    [Fact]
    public void DisabledOrEmptyFilter_MatchesEverything()
    {
        Assert.True(EntraUserFilter.Matches(Props(), null));
        Assert.True(EntraUserFilter.Matches(Props(), new EntraFilter { Enabled = false, Rules = Filter(("companyName", "A")).Rules }));
        Assert.True(EntraUserFilter.Matches(Props(), new EntraFilter { Enabled = true }));
    }

    [Fact]
    public void ValuesOfTheSameProperty_AreOr()
    {
        var filter = Filter(("companyName", "A, B, C"));
        Assert.True(EntraUserFilter.Matches(Props(("companyName", "B")), filter));
        Assert.False(EntraUserFilter.Matches(Props(("companyName", "D")), filter));
    }

    [Fact]
    public void DifferentProperties_AreAnd()
    {
        var filter = Filter(("companyName", "A,B,C"), ("country", "A,D"));
        Assert.True(EntraUserFilter.Matches(Props(("companyName", "C"), ("country", "D")), filter));
        Assert.False(EntraUserFilter.Matches(Props(("companyName", "C"), ("country", "X")), filter));
        Assert.False(EntraUserFilter.Matches(Props(("companyName", "X"), ("country", "A")), filter));
    }

    [Fact]
    public void Comparison_IsCaseInsensitiveAndTrimmed()
    {
        var filter = Filter(("companyName", "epsa"));
        Assert.True(EntraUserFilter.Matches(Props(("CompanyName", "  EPSA ")), filter));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingOrEmptyProperty_NeverMatches(string? value)
    {
        var filter = Filter(("companyName", "A"));
        Assert.False(EntraUserFilter.Matches(Props(("companyName", value)), filter));
        Assert.False(EntraUserFilter.Matches(Props(), filter));
    }
}
