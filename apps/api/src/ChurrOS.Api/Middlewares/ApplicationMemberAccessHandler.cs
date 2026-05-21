using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Share;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ChurrOS.Api.Middlewares
{
    internal record AuthInfoCache(long appId, long accountId, PortDefinition[] ports, bool canExecute, bool canWrite);

    public class ApplicationMemberAccessRequirement : IAuthorizationRequirement
    {

    }

    public class ApplicationMemberAccessHandler : AuthorizationHandler<ApplicationMemberAccessRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationMemberAccessRequirement requirement)
        {
            var cancellationToken = CancellationToken.None;
            var httpContext = context.Resource as DefaultHttpContext;
            if (httpContext is null)
                return;
            var parts = httpContext.Request.Path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var appName = parts[1];
            var portName = parts[2];
            var cacheService = httpContext.RequestServices.GetService<ICacheService>()!;
            var dbContext = httpContext.RequestServices.GetService<ChurrosDbContext>()!;
            var (appId, accountId, ports, canExecute, canWrite) = await cacheService.GetOrAddAsync<AuthInfoCache>($"app:{appName}:user:{context.User?.Identity?.Name}:authinfo", async (ctx) =>
            {
                ctx.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                var conn = dbContext.Database.GetDbConnection();
                var isOpen = conn.State == System.Data.ConnectionState.Open;
                if (!isOpen)
                    await conn.OpenAsync();
                try
                {
                    using var appCmd = conn.CreateCommand();
                    appCmd.CommandText = $"""
                        SELECT a.id, a.account_id, ports, a.acl_id FROM cs.application a
                        WHERE a.name = '{appName}'
                        LIMIT 1;
                        """;
                    var appReader = await appCmd.ExecuteReaderAsync();
                    long accountId = 0;
                    long appId = 0;
                    long appAclId = 0;
                    PortDefinition[] ports = [];
                    if (appReader.Read())
                    {
                        appId = (long)appReader["id"];
                        accountId = (long)appReader["account_id"];
                        appAclId = (long)appReader["acl_id"];
                        ports = JsonSerializer.Deserialize<PortDefinition[]>((string)appReader["ports"], JsonSettings.Value) ?? [];
                        var tenantResolver = httpContext.RequestServices.GetService<ITenantResolver>()!;
                        tenantResolver.SetAccountId(appReader.GetInt64(1));
                        tenantResolver.SetIdentity(context.User?.Identity?.Name ?? string.Empty);
                    }
                    await appReader.DisposeAsync();
                    if (appId <= 0)
                        return default!;
                    var mediator = httpContext.RequestServices.GetService<IMediator>()!;
                    var executeAcls = await mediator.Send(new GetIdentityAcls(dbContext.IdentityId, Models.Dtos.Identity.Permission.Execute), cancellationToken);
                    return new AuthInfoCache(appId, accountId, ports ?? [], executeAcls.ContainsKey(appAclId), executeAcls.ContainsKey(appAclId) && executeAcls[appAclId] != Models.Dtos.Identity.Permission.Execute);
                }
                catch (Exception ex)
                {
                    throw;
                }
                finally
                {
                    if (!isOpen)
                        await conn.CloseAsync();
                }
            }, cancellationToken);

            if (appId == default || accountId == default)
                return;

            var port = ports?.FirstOrDefault(p => p.Name == portName);

            if (port is null)
                return;

            if (port.Authentication == AuthenticationMode.Anonymous)
            {
                context.Succeed(requirement);
                return;
            }

            if (string.IsNullOrWhiteSpace(context.User?.Identity?.Name))
                return;

            if ((port.Sharing == SharingMode.Members && canExecute) ||
                (port.Sharing == SharingMode.None && canWrite))
            {
                context.Succeed(requirement);
            }
        }
    }
}
