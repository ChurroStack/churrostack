using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Services.Share;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrOS.Api.Commands.Environment
{
    public class ConnectEnvironmentHandler : IRequestHandler<ConnectEnvironment, Task>
    {
        private readonly ChurrosDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly RunnerService _runnerService;
        private readonly ProxyConfigurationProvider _proxyConfigurationProvider;
        private readonly ClientNotificationService _clientNotificationService;

        public ConnectEnvironmentHandler(ChurrosDbContext dbContext, IMediator mediator, RunnerService runnerService, ProxyConfigurationProvider proxyConfigurationProvider, ClientNotificationService clientNotificationService)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _runnerService = runnerService;
            _proxyConfigurationProvider = proxyConfigurationProvider;
            _clientNotificationService = clientNotificationService;
        }

        public async Task Handle(ConnectEnvironment request, CancellationToken cancellationToken)
        {
            await _mediator.Send(new EnsureHasRole(IdentityRole.Administrator, _dbContext.IdentityId), cancellationToken);

            var environment = await _dbContext.Set<Domain.Environment>().FirstOrDefaultAsync(o => o.Name == request.Name);

            if (environment is null)
                throw new NotFoundException();

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

            var statusChanged = environment.ProvisionStatus != EnvironmentProvisionStatus.Provisioned;
            environment.ProvisionStatus = EnvironmentProvisionStatus.Provisioned;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _proxyConfigurationProvider.AddEnvironment(environment.Name, environment.Host[1], environment.Port);
            _proxyConfigurationProvider.Reload();

            if (statusChanged)
            {
                await _clientNotificationService.NotifyChangeAsync(environment.AccountId, environment.Name, ClientNotificationService.NotificationTargetType.Environment, cancellationToken);
            }

            await _mediator.Send(new UpdateEnvironmentJobs(environment.Name), cancellationToken);
        }
    }
}
