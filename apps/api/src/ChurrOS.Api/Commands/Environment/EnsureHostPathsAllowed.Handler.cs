using ChurrOS.Api.Data;
using DispatchR;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public class EnsureHostPathsAllowedHandler : IRequestHandler<EnsureHostPathsAllowed, ValueTask<bool>>
    {
        private readonly IMediator _mediator;
        private readonly ChurrosDbContext _context;
        private readonly ILogger<EnsureHostPathsAllowedHandler> _logger;

        public EnsureHostPathsAllowedHandler(IMediator mediator, ChurrosDbContext context, ILogger<EnsureHostPathsAllowedHandler> logger)
        {
            _mediator = mediator;
            _context = context;
            _logger = logger;
        }

        public async ValueTask<bool> Handle(EnsureHostPathsAllowed request, CancellationToken cancellationToken)
        {
            var requested = request.RequestedHostPaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (requested.Length == 0)
            {
                return true;
            }

            var allowed = await _mediator.Send(new GetAllowedHostPaths(request.EnvironmentName), cancellationToken);
            var allowedSet = new HashSet<string>(allowed.Select(a => a.Path), StringComparer.Ordinal);

            foreach (var path in requested)
            {
                if (!allowedSet.Contains(path))
                {
                    _logger.LogWarning("Storage hostPath denied env={Env} identity={Identity} path={HostPath} reason=not-allowed",
                        request.EnvironmentName, _context.IdentityId, path);
                    throw new UnauthorizedAccessException($"You do not have access to host path '{path}'.");
                }
                _logger.LogInformation("Storage hostPath allowed env={Env} identity={Identity} path={HostPath}",
                    request.EnvironmentName, _context.IdentityId, path);
            }

            return true;
        }
    }
}
