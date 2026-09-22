using ChurrOS.Api.Services;

namespace ChurrOS.Api.Middlewares
{
    public class MultiTenantMiddleware
    {
        private readonly RequestDelegate _next;

        public MultiTenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, IAccountMembershipResolver accountMembershipResolver, ILogger<MultiTenantMiddleware> logger)
        {
            // If user is authenticated, resolve AccountId
            if (httpContext.User?.Identity?.IsAuthenticated ?? false)
            {
                var identityName = httpContext.User.Identity.Name;
                var requestedAccountId = httpContext.Request.Headers.TryGetValue("X-TENANT-ID", out var accountIdHeader)
                    && long.TryParse(accountIdHeader, out var accountId)
                        ? accountId
                        : (long?)null;
                var tenantSource = requestedAccountId.HasValue ? "header" : "none";

                // Bearer-token callers (MCP clients, any pure API client) have no way to send the
                // X-TENANT-ID header a browser session can. The token's own "tid" claim (set by
                // OAuthController.Exchange at issuance time) is the caller's actual intended tenant,
                // so fall back to it before defaulting to "pick the identity's only account" --
                // without this, any identity belonging to more than one account gets a blanket 403
                // on every bearer-token request, including every /mcp call. ResolveAccountIdAsync
                // still re-verifies membership against the database, so this doesn't bypass
                // authorization -- it only supplies a better default when the header is absent.
                if (!requestedAccountId.HasValue)
                {
                    var tidClaim = httpContext.User.FindFirst("tid")?.Value;
                    if (long.TryParse(tidClaim, out var tidAccountId))
                    {
                        requestedAccountId = tidAccountId;
                        tenantSource = "tid_claim";
                    }
                }

                logger.LogDebug(
                    "[TenantResolution] identity={IdentityName} source={Source} requestedAccountId={AccountId}",
                    identityName, tenantSource, requestedAccountId);

                var userAccountId = await accountMembershipResolver.ResolveAccountIdAsync(identityName!, requestedAccountId, httpContext.RequestAborted);

                if (!userAccountId.HasValue || userAccountId <= 0)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await httpContext.Response.WriteAsync("Access denied.");
                    return;
                }
                else
                {
                    httpContext.Items["AccountId"] = userAccountId;
                }
            }

            // Call the next middleware in the pipeline
            await _next(httpContext);
        }
    }
}
