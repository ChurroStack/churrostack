using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Services.Security;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Stream;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace ChurrOS.Api.Commands.Applications
{
    public class WatchApplicationConsoleHandler : IStreamRequestHandler<WatchApplicationConsole, string>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly RunnerService _runnerService;

        public WatchApplicationConsoleHandler(
            IMediator mediator,
            ChurrosDbContext context,
            RunnerService runnerService)
        {
            _mediator = mediator;
            _context = context;
            _runnerService = runnerService;
        }

        public async IAsyncEnumerable<string> Handle(WatchApplicationConsole request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .Include(o => o.Environment)
                .Include(o => o.Deployments)
                .Select(o => new { o.Id, o.Name, o.AclId, o.Environment, o.Deployments })
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Write), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to watch this application.");
            }

            var deployment = app.Deployments?.FirstOrDefault(o => o.Name == request.DeploymentName);
            if (deployment == null)
            {
                throw new NotFoundException($"Deployment with name '{request.DeploymentName}' was not found in application '{request.Name}'.");
            }

            var parts = app.Environment!.EncryptionKey.Split(':');
            var encryptionKey = AesGcmEncryption.Decrypt(parts[0], _context.AccountEncryptionKey, parts[1]);
            var client = _runnerService.CreateClient(app.Environment!.Host[1], app.Environment.Name, app.Environment.Port, encryptionKey);

            await foreach (var line in client.WatchConsoleAsync(deployment.Name, cancellationToken))
            {
                yield return line;
            }
        }
    }
}
