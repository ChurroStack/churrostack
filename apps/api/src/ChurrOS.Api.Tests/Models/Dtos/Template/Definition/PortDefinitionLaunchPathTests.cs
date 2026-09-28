using System.Text.Json;
using ChurrOS.Api.Mappers;
using ChurrOS.Api.Models.Dtos.Share;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Utils;
using Mapster;

namespace ChurrOS.Api.Tests.Models.Dtos.Template.Definition;

/// <summary>
/// LaunchPath is threaded through three independent binding paths that a passing build does
/// not exercise: the jsonb column round-trip (PortDefinition), the PATCH request body
/// (PortDefinitionItem), and the Mapster projection from one to the other used by
/// GetApplicationByName/UpdateApplication to build the API response. Each binds via the
/// class's sole constructor, so a parameter added without matching the property name would
/// silently deserialize/map as null instead of failing the build.
/// </summary>
public class PortDefinitionLaunchPathTests
{
    private static readonly JsonSerializerOptions Options = new();

    static PortDefinitionLaunchPathTests()
    {
        Options.ApplyDefaultOptions();
    }

    private static PortDefinition NewPortDefinition(string? launchPath) =>
        new("web", "Web", "lucide:globe", "desc", 8000, ProtocolType.Web, null, null, AuthenticationMode.Oidc, SharingMode.Members, null, launchPath);

    [Fact]
    public void PortDefinition_JsonRoundTrip_PreservesLaunchPath()
    {
        var original = NewPortDefinition("/a/b/c?q=v&x=6");

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<PortDefinition>(json, Options);

        Assert.Contains("launchPath", json);
        Assert.Equal("/a/b/c?q=v&x=6", roundTripped!.LaunchPath);
    }

    [Fact]
    public void PortDefinition_JsonWithoutLaunchPathKey_DeserializesToNull()
    {
        // Simulates a row persisted before this field existed.
        const string json = """{"name":"web","title":"Web","icon":"lucide:globe","description":"desc","port":8000,"protocol":"web","authentication":"oidc","sharing":"members"}""";

        var result = JsonSerializer.Deserialize<PortDefinition>(json, Options);

        Assert.Null(result!.LaunchPath);
    }

    [Fact]
    public void PortDefinitionItem_JsonWithLaunchPath_Deserializes()
    {
        // Simulates the PATCH /api/applications/{name} request body for the "ports" entry.
        const string json = """{"name":"web","title":"Web","icon":"lucide:globe","description":"desc","protocol":"web","authentication":"oidc","sharing":"members","port":8000,"launchPath":"/a/b/c?q=v"}""";

        var result = JsonSerializer.Deserialize<PortDefinitionItem>(json, Options);

        Assert.Equal("/a/b/c?q=v", result!.LaunchPath);
    }

    [Fact]
    public void PortDefinitionItem_JsonWithoutLaunchPathKey_DeserializesToNull()
    {
        const string json = """{"name":"web","title":"Web","icon":"lucide:globe","description":"desc","protocol":"web","authentication":"oidc","sharing":"members","port":8000}""";

        var result = JsonSerializer.Deserialize<PortDefinitionItem>(json, Options);

        Assert.Null(result!.LaunchPath);
    }

    [Fact]
    public void Mapster_PortDefinitionToPortDefinitionItem_CarriesLaunchPath()
    {
        var config = new TypeAdapterConfig();
        new ApplicationMapper().Register(config);

        var source = NewPortDefinition("/a/b/c?q=v");

        var mapped = source.Adapt<PortDefinitionItem>(config);

        Assert.Equal("/a/b/c?q=v", mapped.LaunchPath);
    }
}
