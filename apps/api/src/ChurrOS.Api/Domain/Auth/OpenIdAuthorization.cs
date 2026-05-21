using OpenIddict.EntityFrameworkCore.Models;

namespace ChurrOS.Api.Domain.Auth
{
    public class OpenIdAuthorization : OpenIddictEntityFrameworkCoreAuthorization<Guid, OpenIdApplication, OpenIdToken>
    {
    }
}
