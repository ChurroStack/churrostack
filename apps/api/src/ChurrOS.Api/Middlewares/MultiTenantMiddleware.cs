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

        public async Task Invoke(HttpContext httpContext, IAccountMembershipResolver accountMembershipResolver)
        {
            // If user is authenticated, resolve AccountId
            if (httpContext.User?.Identity?.IsAuthenticated ?? false)
            {
                var identityName = httpContext.User.Identity.Name;
                var requestedAccountId = httpContext.Request.Headers.TryGetValue("X-TENANT-ID", out var accountIdHeader)
                    && long.TryParse(accountIdHeader, out var accountId)
                        ? accountId
                        : (long?)null;
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
