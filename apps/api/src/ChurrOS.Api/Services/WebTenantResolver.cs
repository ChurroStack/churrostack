using ChurrOS.Api.Utils;
using System.Security.Claims;

namespace ChurrOS.Api.Services
{
    public class WebTenantResolver : ITenantResolver
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private long? _accountId;
        private ClaimsIdentity? _identity;
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Identity?.Name);

        public long AccountId
        {
            get
            {
                if (_accountId is null)
                {
                    if (_httpContextAccessor?.HttpContext == null)
                    {
                        throw new UnauthorizedAccessException("Cannot access HttpContext");
                    }
                    try
                    {
                        var httpContext = _httpContextAccessor.HttpContext;
                        if (!httpContext.Items.ContainsKey("AccountId"))
                        {
                            throw new UnauthorizedAccessException("Tenant not set");
                        }
                        _accountId = (long)httpContext.Items["AccountId"]!;
                        return _accountId.Value;
                    }
                    catch
                    {
                        throw new UnauthorizedAccessException("Tenant not set");
                    }
                }
                return _accountId.Value;
            }
        }

        public ClaimsIdentity Identity
        {
            get
            {
                if (_identity != null)
                    return _identity;
                _identity = (ClaimsIdentity?)_httpContextAccessor?.HttpContext?.User?.Identity;
                if (_identity == null || string.IsNullOrWhiteSpace(_identity.Name))
                    throw new ArgumentException("Identity cannot be null in HttpContext.");
                return _identity;
            }
        }

        public WebTenantResolver(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetIdentity(string identity)
        {
            _identity = identity.ToClaimIdentity();
        }

        public void SetAccountId(long accountId)
        {
            _accountId = accountId;
        }
    }
}
