using OpenIddict.EntityFrameworkCore.Models;

namespace ChurrOS.Api.Domain.Auth
{
    public class OpenIdApplication : OpenIddictEntityFrameworkCoreApplication<Guid, OpenIdAuthorization, OpenIdToken>
    {
    }
}
