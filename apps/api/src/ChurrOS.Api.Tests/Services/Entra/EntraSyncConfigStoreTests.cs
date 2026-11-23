using ChurrOS.Api.Services.Entra;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ChurrOS.Api.Tests.Services.Entra;

public class EntraSyncConfigStoreTests
{
    private static IConfiguration Config(string? defaultTenant = null, bool app = true)
    {
        var values = new Dictionary<string, string?>();
        if (defaultTenant is not null) values["ExternalProviders:Microsoft:TenantId"] = defaultTenant;
        if (app)
        {
            values["ExternalProviders:Microsoft:ClientId"] = "id";
            values["ExternalProviders:Microsoft:ClientSecret"] = "secret";
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IDictionary<string, object> Meta(string? tenant) => new Dictionary<string, object>
    {
        ["entraTenantId"] = JsonSerializer.SerializeToElement(tenant)
    };

    [Fact]
    public void AccountTenant_WinsOverTheServerDefault()
        => Assert.Equal("acct.onmicrosoft.com", EntraSyncConfigStore.ResolveTenantId(Meta(" acct.onmicrosoft.com "), Config("default.onmicrosoft.com")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoAccountTenant_FallsBackToTheServerDefault(string? accountValue)
    {
        Assert.Equal("default.onmicrosoft.com", EntraSyncConfigStore.ResolveTenantId(Meta(accountValue), Config("default.onmicrosoft.com")));
        Assert.Equal("default.onmicrosoft.com", EntraSyncConfigStore.ResolveTenantId(null, Config("default.onmicrosoft.com")));
        Assert.Equal("default.onmicrosoft.com", EntraSyncConfigStore.ResolveTenantId(new Dictionary<string, object>(), Config("default.onmicrosoft.com")));
    }

    [Fact]
    public void NeitherConfigured_ResolvesNothing()
        => Assert.Null(EntraSyncConfigStore.ResolveTenantId(null, Config()));

    [Theory]
    [InlineData("evil/../tenant")]
    [InlineData("a b")]
    [InlineData("x?y=1")]
    public void MalformedAccountTenant_IsAMisconfiguration_NotAFallback(string value)
        => Assert.Null(EntraSyncConfigStore.ResolveTenantId(Meta(value), Config("default.onmicrosoft.com")));

    [Fact]
    public void Availability_NeedsTheAppRegistrationAndATenant()
    {
        Assert.True(EntraSyncConfigStore.IsAvailable(Meta("acct.onmicrosoft.com"), Config()));
        Assert.True(EntraSyncConfigStore.IsAvailable(null, Config("default.onmicrosoft.com")));
        Assert.False(EntraSyncConfigStore.IsAvailable(null, Config()));
        Assert.False(EntraSyncConfigStore.IsAvailable(Meta("acct.onmicrosoft.com"), Config(app: false)));
    }

    [Fact]
    public void Merge_KeepsOtherAccountMetadata_IncludingTheOperatorTenant()
    {
        var metadata = new Dictionary<string, object> { ["quotas"] = 1, ["entraTenantId"] = JsonSerializer.SerializeToElement("acct.onmicrosoft.com") };
        var merged = EntraSyncConfigStore.Merge(metadata, new ChurrOS.Api.Models.Dtos.IdentitySync.EntraSyncConfig { Enabled = true });

        Assert.Contains("quotas", merged.Keys);
        Assert.Equal("acct.onmicrosoft.com", EntraSyncConfigStore.ResolveTenantId(merged, Config()));
    }
}
