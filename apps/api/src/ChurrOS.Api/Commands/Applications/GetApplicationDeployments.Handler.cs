using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationDeploymentsHandler : IRequestHandler<GetApplicationDeployments, ValueTask<QueryResult<ApplicationDeploymentItem>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ITenantResolver _tenantResolver;

        public GetApplicationDeploymentsHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator, IConnectionMultiplexer connectionMultiplexer, ITenantResolver tenantResolver)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
            _connectionMultiplexer = connectionMultiplexer;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<QueryResult<ApplicationDeploymentItem>> Handle(GetApplicationDeployments request, CancellationToken cancellationToken)
        {
            var repo = _context.Set<Domain.Application>();

            var app = await repo
                .AsNoTracking()
                .Include(o => o.Environment)
                .Include(o => o.Deployments)
                .FirstOrDefaultAsync(o => o.Name == request.Name);

            if (app == null)
            {
                throw new NotFoundException($"Application with name '{request.Name}' was not found.");
            }

            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                if (!identityAcls.ContainsKey(app.AclId) && !identityAcls.ContainsKey(app.Environment!.AclId))
                    throw new UnauthorizedAccessException("You do not have permission to read this application.");
            }

            var db = _connectionMultiplexer.GetDatabase();
            var deployments = app.Deployments ?? [];
            var result = new List<ApplicationDeploymentItem>();

            var identityIds = deployments.Select(o => o.OwnerId).Where(o => o.HasValue).Distinct().ToArray();

            var identities = await _context.Set<Domain.Identity>()
                .Where(o => identityIds.Contains(o.Id))
                .Select(o => new { o.Id, o.Name, o.DisplayName, o.Type, o.Role })
                .ToDictionaryAsync(o => o.Id, o => o);

            foreach (var deployment in deployments)
            {
                var deploymentItem = _mapper.Map<ApplicationDeploymentItem>(deployment);
                var usageKey = $"churros_tenant:{_tenantResolver.AccountId}:app:{app.Id}:resource_usage:{deployment.Name}";
                var values = await db.HashGetAllAsync(usageKey);
                var metrics = new Dictionary<string, double>();
                foreach (var value in values ?? [])
                {
                    switch (value.Name)
                    {
                        case "cpu":
                            metrics.TryAdd("cpu_usage", (double)value.Value);
                            break;
                        case "gpu":
                            metrics.TryAdd("gpu_usage", (double)value.Value);
                            break;
                        case "memory":
                            metrics.TryAdd("memory_usage", (double)value.Value);
                            break;
                        case "storage":
                            metrics.TryAdd("storage_usage", (double)value.Value);
                            break;
                    }
                }
                deploymentItem.Metrics = metrics;
                if (deployment.OwnerId.HasValue && identities.TryGetValue(deployment.OwnerId.Value, out var identity))
                {
                    deploymentItem.Owner = new IdentitySummary(identity.Name, identity.DisplayName, identity.Type, identity.Role);
                }
                result.Add(deploymentItem);
            }

            return new QueryResult<ApplicationDeploymentItem>(result);
        }
    }
}
