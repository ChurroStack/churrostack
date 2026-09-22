using System.Collections.Immutable;
using ChurrOS.Api.Controllers;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ChurrOS.Api.Tests.Controllers;

public class OAuthControllerConsentTests
{
    // The gap this closes: a DCR-registered client (granted only scp:mcp) could otherwise request
    // scope=mcp api/.default at /authorize and, on one Allow click, receive a token whose audience
    // covers the general REST API -- see the caller's comment in OAuthController.HandleAuthorizeAsync.
    [Fact]
    public void GetDeniedScopes_ReturnsOnlyScopesWithoutAMatchingPermission()
    {
        var permissions = ImmutableArray.Create("ept:authorization", "ept:token", "scp:mcp");

        var denied = OAuthController.GetDeniedScopes(permissions, ["mcp", "api/.default"]);

        Assert.Equal(["api/.default"], denied);
    }

    [Fact]
    public void GetDeniedScopes_ReturnsEmpty_WhenEveryRequestedScopeIsPermitted()
    {
        var permissions = ImmutableArray.Create("scp:mcp", "scp:offline_access");

        var denied = OAuthController.GetDeniedScopes(permissions, ["mcp", "offline_access"]);

        Assert.Empty(denied);
    }

    [Fact]
    public void GetDeniedScopes_ReturnsEmpty_WhenNoScopesAreRequested()
    {
        var permissions = ImmutableArray.Create("scp:mcp");

        var denied = OAuthController.GetDeniedScopes(permissions, []);

        Assert.Empty(denied);
    }

    [Fact]
    public void RequiresConsentPrompt_NeverPrompts_ForImplicitConsentType()
    {
        Assert.False(OAuthController.RequiresConsentPrompt(ConsentTypes.Implicit, consentGranted: false, existingAuthorizations: 0, promptConsent: false));
        Assert.False(OAuthController.RequiresConsentPrompt(ConsentTypes.Implicit, consentGranted: false, existingAuthorizations: 5, promptConsent: true));
    }

    [Fact]
    public void RequiresConsentPrompt_AlwaysPrompts_ForSystematicConsentType()
    {
        Assert.True(OAuthController.RequiresConsentPrompt(ConsentTypes.Systematic, consentGranted: false, existingAuthorizations: 5, promptConsent: false));
    }

    [Fact]
    public void RequiresConsentPrompt_ForExplicitConsentType_PromptsOnlyWithoutAPriorAuthorization_OrWhenReconsentIsRequested()
    {
        Assert.True(OAuthController.RequiresConsentPrompt(ConsentTypes.Explicit, consentGranted: false, existingAuthorizations: 0, promptConsent: false));
        Assert.False(OAuthController.RequiresConsentPrompt(ConsentTypes.Explicit, consentGranted: false, existingAuthorizations: 1, promptConsent: false));
        Assert.True(OAuthController.RequiresConsentPrompt(ConsentTypes.Explicit, consentGranted: false, existingAuthorizations: 1, promptConsent: true));
    }

    [Fact]
    public void RequiresConsentPrompt_NeverPrompts_OnceConsentHasJustBeenGranted()
    {
        Assert.False(OAuthController.RequiresConsentPrompt(ConsentTypes.Systematic, consentGranted: true, existingAuthorizations: 0, promptConsent: true));
        Assert.False(OAuthController.RequiresConsentPrompt(ConsentTypes.Explicit, consentGranted: true, existingAuthorizations: 0, promptConsent: false));
    }
}
