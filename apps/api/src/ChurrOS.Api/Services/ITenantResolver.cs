using System.Security.Claims;

namespace ChurrOS.Api.Services
{
    public interface ITenantResolver
    {
        long AccountId { get; }
        ClaimsIdentity? Identity { get; }
        bool IsAuthenticated { get; }
        void SetAccountId(long id);
        void SetIdentity(string identity);
    }
}
