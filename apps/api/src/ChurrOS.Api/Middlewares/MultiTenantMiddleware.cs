using ChurrOS.Api.Data;
using LazyCache;

namespace ChurrOS.Api.Middlewares
{
    public class MultiTenantMiddleware
    {
        private readonly RequestDelegate _next;

        public MultiTenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, IAppCache appCache, ChurrosDbContext dbContext)
        {
            // If user is authenticated, resolve AccountId
            if (httpContext.User?.Identity?.IsAuthenticated ?? false)
            {
                var identityName = httpContext.User.Identity.Name;
                long? userAccountId = await appCache.GetOrAddAsync<long?>($"identity:{identityName}", async (ctx) =>
                {
                    try
                    {
                        if (httpContext.Request.Headers.TryGetValue("X-TENANT-ID", out var accountIdHeader) && long.TryParse(accountIdHeader, out var accountId))
                        {
                            var count = await dbContext.ExecuteScalarAsync<long?>($"SELECT COUNT(1) FROM cs.identity WHERE account_id = {accountId} AND name = {identityName}");
                            return accountId;
                        }
                        else
                        {
                            var accountIds = await dbContext.ExecuteQueryAsync<long>($"SELECT account_id FROM cs.identity WHERE name = {identityName}");
                            if (accountIds?.Count == 1)
                            {
                                return accountIds[0];
                            }
                        }
                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                });

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
