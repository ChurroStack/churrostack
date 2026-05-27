using ChurrOS.Api.Commands.Environment;
using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Commands.Template;
using ChurrOS.Api.Data;
using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NotFoundException = ChurrOS.Api.Utils.Exceptions.NotFoundException;

namespace ChurrOS.Api.Commands.Applications
{
    public class UpdateApplicationHandler : IRequestHandler<UpdateApplication, ValueTask<ApplicationItem>>
    {
        private readonly IMediator _mediator;
        private readonly IMapper _mapper;
        private readonly ChurrosDbContext _context;
        private readonly ITenantResolver _tenantResolver;
        private readonly ClientNotificationService _clientNotificationService;
        private readonly ICacheService _cacheService;
        private readonly ILockService _lockService;

        public UpdateApplicationHandler(IMediator mediator, ChurrosDbContext context, IMapper mapper, ITenantResolver tenantResolver, ClientNotificationService clientNotificationService, ICacheService cacheService, ILockService lockService)
        {
            _mediator = mediator;
            _context = context;
            _mapper = mapper;
            _tenantResolver = tenantResolver;
            _clientNotificationService = clientNotificationService;
            _cacheService = cacheService;
            _lockService = lockService;
        }

        public async ValueTask<ApplicationItem> Handle(UpdateApplication request, CancellationToken cancellationToken)
        {
            var app = await _context.Set<Domain.Application>()
                .Include(o => o.Environment)
                .Include(o => o.Extensions)
                .Include(o => o.Template)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.Environment!.AclId) && !identityAcls.ContainsKey(app!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to update this application.");
            }

            var membersToPurge = await _context.Set<Domain.AclMember>()
                .Include(o => o.Identity)
                .Where(o => o.AclId == app.AclId)
                .Select(o => o.Identity!.Id)
                .ToListAsync(cancellationToken);

            // If size is being updated and the app currently has any Running/Starting deployment,
            // serialize the size change against StartApplication calls on the same environment so
            // a concurrent start can't squeeze in between our quota check and SaveChangesAsync.
            IAsyncDisposable? envLock = null;
            var enforceSizeQuota = false;
            if (request.Body.TryGetProperty("size", out _))
            {
                enforceSizeQuota = await _context.Set<Domain.ApplicationDeployment>()
                    .AnyAsync(d => d.ApplicationId == app.Id
                                && (d.ExecutionStatus == DeploymentExecutionStatus.Running
                                 || d.ExecutionStatus == DeploymentExecutionStatus.Starting), cancellationToken);
                if (enforceSizeQuota)
                {
                    var lockKey = $"churros_tenant:{_tenantResolver.AccountId}:env:{app.EnvironmentId}:resource_lock";
                    envLock = await _lockService.AcquireAsync(lockKey, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(5), cancellationToken)
                        ?? throw new InvalidOperationException("Environment is busy, please retry.");
                }
            }

            long[]? updatedMembers = null;
            try
            {
                foreach (var entry in request.Body.EnumerateObject())
                {
                    switch (entry.Name)
                    {
                        case "variables":
                            app.Variables = entry.Value.Deserialize<ApplicationEnvironmentVariable[]>(JsonSettings.Value)!;
                            break;
                        case "size":
                            {
                                var newSize = entry.Value.Deserialize<SizeRequestItem>(JsonSettings.Value)!;
                                if (enforceSizeQuota)
                                {
                                    await _mediator.Send(new EnsureEnvironmentRunningQuota(app.EnvironmentId, app.Id, newSize, EnsureRunningQuotaMode.Update), cancellationToken);
                                }
                                app.Size = newSize;
                                break;
                            }
                        case "parameters":
                            app.Parameters = entry.Value.Deserialize<IDictionary<string, string[]>>(JsonSettings.Value)!;
                            break;
                        case "extensions":
                            await UpdateExtensionsAsync(app, entry.Value.Deserialize<ApplicationExtensionItem[]>(JsonSettings.Value), cancellationToken);
                            break;
                        case "ports":
                            await UpdatePortsAsync(app, entry.Value.Deserialize<PortDefinitionItem[]>(JsonSettings.Value));
                            break;
                        case "members":
                            {
                                if (!await _mediator.Send(new IsAdminOrHasAcl(app.AclId, Permission.Manage), cancellationToken))
                                    throw new UnauthorizedAccessException("You do not have permission to manage this application security members.");

                                var newMembers = entry.Value.Deserialize<MemberItem[]>(JsonSettings.Value)!;
                                updatedMembers = await _mediator.UpdateAclAsync(membersToPurge, _context, _tenantResolver.AccountId, app.AclId, newMembers, cancellationToken);
                                break;
                            }
                        case "metadata":
                            app.Metadata = entry.Value.Deserialize<JsonElement>(JsonSettings.Value)!;
                            break;
                        case "tags":
                            app.Tags = TagsHelper.Normalize(entry.Value.Deserialize<string[]>(JsonSettings.Value));
                            break;
                        default:
                            throw new ArgumentException($"Cannot update member '{entry.Name}' for this application.");
                    }
                }

                app.ModifiedAt = DateTimeOffset.Now;
                app.ModifiedById = _context.IdentityId;

                await _context.SaveChangesAsync();
            }
            finally
            {
                if (envLock != null)
                    await envLock.DisposeAsync();
            }

            foreach (var identityId in new long[0].Union(membersToPurge ?? []).Union(updatedMembers ?? []).Distinct())
            {
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityId}");
            }

            await _cacheService.InvalidatePrefixAsync($"app:{app.Name}");

            await _clientNotificationService.NotifyChangeAsync(_tenantResolver.AccountId, app.Name, ClientNotificationService.NotificationTargetType.Application, cancellationToken);

            return _mapper.Map<Domain.Application, ApplicationItem>(app);
        }

        private async Task UpdateExtensionsAsync(Application app, ApplicationExtensionItem[]? applicationExtensionItems, CancellationToken cancellationToken)
        {
            var repo = _context.Set<ApplicationExtension>();
            if (app.Extensions is not null)
            {
                // Delete extensions
                var names = applicationExtensionItems?.Select(o => o.Name) ?? [];
                repo.RemoveRange(app.Extensions.Where(o => !names.Contains(o.Name)));
            }
            foreach (var ext in applicationExtensionItems ?? [])
            {
                var existingExtension = app.Extensions?.FirstOrDefault(o => o.Name == ext.Name);
                if (existingExtension is not null)
                {
                    existingExtension.Parameters = ext.Parameters;
                    existingExtension.Enabled = ext.Enabled;
                }
                else
                {
                    var now = DateTimeOffset.Now;
                    var template = app.Template?.Definition?.Extensions?.First(o => o.Name == ext.Name);
                    if (template is not null && app.Environment is not null)
                    {
                        var templateId = await _mediator.Send(new GetTemplateIdByName(template.Template, app.Environment.Type), cancellationToken);
                        repo.Add(new ApplicationExtension(_tenantResolver.AccountId, app.EnvironmentId, app.Id, templateId, ext.Name, ext.Enabled, ext.Parameters, now, _context.IdentityId, now, _context.IdentityId));
                    }
                }
            }
        }

        private async Task UpdatePortsAsync(Application app, PortDefinitionItem[]? portDefinitionItem)
        {
            foreach (var port in portDefinitionItem ?? [])
            {
                var existingPort = app.Ports?.FirstOrDefault(p => p.Name == port.Name);
                if (existingPort is null)
                    throw new ArgumentException($"Port '{port.Name}' not found in application '{app.Name}'.");
                existingPort.Authentication = port.Authentication;
                existingPort.Sharing = port.Sharing;
                existingPort.Port = port.Port.HasValue && port.Port > 0 ? port.Port.Value : existingPort.Port;
            }
            app.Ports = JsonSerializer.SerializeToElement(app.Ports ?? [], JsonSettings.Value).Deserialize<PortDefinition[]>(JsonSettings.Value);
        }
    }
}
