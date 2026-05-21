using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Services.Share;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class UpdateEnvironmentHandler : IRequestHandler<UpdateEnvironment, ValueTask<EnvironmentItem>>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly RunnerService _runnerService;
        private readonly ProxyConfigurationProvider _proxyConfigurationProvider;
        private readonly ClientNotificationService _clientNotificationService;
        private readonly ITenantResolver _tenantResolver;
        private readonly ICacheService _cacheService;

        public UpdateEnvironmentHandler(
            ChurrosDbContext dbContext,
            IMediator mediator,
            RunnerService runnerService,
            ProxyConfigurationProvider proxyConfigurationProvider,
            ClientNotificationService clientNotificationService,
            ITenantResolver tenantResolver,
            ICacheService cacheService)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _runnerService = runnerService;
            _proxyConfigurationProvider = proxyConfigurationProvider;
            _clientNotificationService = clientNotificationService;
            _tenantResolver = tenantResolver;
            _cacheService = cacheService;
        }

        public async ValueTask<EnvironmentItem> Handle(UpdateEnvironment request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var environment = await _dbContext.Set<Domain.Environment>().FirstOrDefaultAsync(o => o.Name == request.Name);

            if (environment is null)
                throw new NotFoundException();

            environment.ModifiedAt = DateTimeOffset.Now;
            environment.ModifiedById = _dbContext.IdentityId;

            var parts = environment.EncryptionKey.Split(':');
            var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _dbContext.AccountEncryptionKey, parts[1]);
            var client = _runnerService.CreateClient(environment.Host[1], environment.Name, environment.Port, encryptionKey);
            var envDef = await client.ConnectAsync(cancellationToken);
            environment.Definition = envDef;
            environment.ProvisionStatus = EnvironmentProvisionStatus.Provisioning;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var templates = await _dbContext.Set<Domain.Template>().Where(o => o.Target == environment.Type)
                .Select(o => o.Content)
                .ToListAsync();

            foreach (var template in templates)
            {
                await client.RegisterTemplateAsync(template, cancellationToken);
            }

            environment.ProvisionStatus = EnvironmentProvisionStatus.Provisioned;

            long[]? updatedMembers = null;
            List<long>? membersToPurge = null;
            if (request.Body.Members != null && request.Body.Members.Any())
            {
                membersToPurge = await _dbContext.Set<Domain.AclMember>()
                    .Include(o => o.Identity)
                    .Where(o => o.AclId == environment.AclId)
                    .Select(o => o.Identity!.Id)
                    .ToListAsync(cancellationToken);

                updatedMembers = await _mediator.UpdateAclAsync(membersToPurge, _dbContext, _tenantResolver.AccountId, environment.AclId, request.Body.Members, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _proxyConfigurationProvider.AddEnvironment(environment.Name, environment.Host[1], environment.Port);
            _proxyConfigurationProvider.Reload();

            await _clientNotificationService.NotifyChangeAsync(environment.AccountId, environment.Name, ClientNotificationService.NotificationTargetType.Environment, cancellationToken);

            foreach (var identityId in Array.Empty<long>().Union(membersToPurge ?? []).Union(updatedMembers ?? []).Distinct())
            {
                await _cacheService.InvalidatePrefixAsync($"tenant:{_tenantResolver.AccountId}:identity:{identityId}");
            }

            return await _mediator.Send(new GetEnvironmentByName(request.Name), cancellationToken);
        }
    }
}
