using OpenIddict.EntityFrameworkCore.Models;

namespace ChurrOS.Api.Domain.Auth
{
    /// <summary>
    /// Represents an OpenId token.
    /// </summary>
    public class OpenIdToken : OpenIddictEntityFrameworkCoreToken<Guid, OpenIdApplication, OpenIdAuthorization>
    {
    }
}
