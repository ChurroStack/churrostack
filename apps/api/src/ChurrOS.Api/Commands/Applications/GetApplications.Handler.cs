using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChurrOS.Api.Commands.Applications
{
    public class GetApplicationsHandler : IRequestHandler<GetApplications, ValueTask<QueryResult<ApplicationSummary>>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly ITenantResolver _tenantResolver;

        public GetApplicationsHandler(ChurrosDbContext context, IMapper mapper, IMediator mediator, IConnectionMultiplexer connectionMultiplexer, ITenantResolver tenantResolver)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
            _connectionMultiplexer = connectionMultiplexer;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<QueryResult<ApplicationSummary>> Handle(GetApplications request, CancellationToken cancellationToken)
        {
            var isAdmin = await _mediator.Send(new HasRole(IdentityRole.Administrator, _context.IdentityId), cancellationToken);
            long[]? aclIds = null;
            List<long>? envIds = null;
            if (!isAdmin)
            {
                var identityAcls = await _mediator.Send(new GetIdentityAcls(_context.IdentityId, Permission.Read), cancellationToken);
                aclIds = identityAcls.Keys.ToArray();
                envIds = await _context.Set<Domain.Environment>()
                    .AsNoTracking()
                    .Where(o => aclIds.Contains(o.AclId))
                    .Select(o => o.Id)
                    .ToListAsync();
            }

            IQueryable<Domain.Application> query = _context.Set<Domain.Application>()
                .AsNoTracking()
                .Include(o => o.Deployments)
                .Include(o => o.CreatedBy)
                .Include(o => o.ModifiedBy)
                .Include(o => o.Template)
                .Include(o => o.Environment);

            if (!isAdmin)
                query = query.Where(o => aclIds!.Contains(o.AclId) || envIds!.Contains(o.EnvironmentId));

            if (!string.IsNullOrWhiteSpace(request.Query?.Search))
            {
                query = query.Where(o => o.Name!.Contains(request.Query.Search));
            }

            if (!string.IsNullOrWhiteSpace(request.Query?.Environment))
            {
                query = query.Where(o => o.Environment!.Name == request.Query.Environment);
            }

            if (request.Query?.Mode.HasValue ?? false)
            {
                var mode = request.Query.Mode;
                query = query.Where(o => o.Mode == mode);
            }

            if (!string.IsNullOrWhiteSpace(request.Query?.CreatedBy))
            {
                var createdBy = request.Query.CreatedBy;
                query = query.Where(o => o.CreatedBy!.Name == createdBy);
            }

            var count = await query.CountAsync(cancellationToken);

            query = request.Query?.ApplyPaginationTo(query) ?? query;

            var items = query.Select(o => new { o.Id, o.Name, o.Template, o.Mode, o.Deployments, o.Size, o.Environment, o.CreatedAt, o.CreatedBy, o.ModifiedAt, o.ModifiedBy });

            var result = new List<ApplicationSummary>();
            var db = _connectionMultiplexer.GetDatabase();
            await foreach (var app in items.ToAsyncEnumerable())
            {
                var summary = _mapper.Map<ApplicationSummary>(app);
                foreach (var deployment in app.Deployments ?? [])
                {
                    switch (deployment.ProvisionStatus)
                    {
                        case Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioning:
                            if (summary.ProvisionStatus == Models.Dtos.Deployment.DeploymentProvisionStatus.Pending)
                            {
                                summary.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioning;
                            }
                            break;
                        case Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned:
                            if (summary.ProvisionStatus != Models.Dtos.Deployment.DeploymentProvisionStatus.Failed)
                            {
                                summary.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Provisioned;
                            }
                            break;
                        case Models.Dtos.Deployment.DeploymentProvisionStatus.Failed:
                            summary.ProvisionStatus = Models.Dtos.Deployment.DeploymentProvisionStatus.Failed;
                            break;
                    }
                    switch (deployment.ExecutionStatus)
                    {
                        case Models.Dtos.Deployment.DeploymentExecutionStatus.Starting:
                            if (summary.ExecutionStatus == Models.Dtos.Deployment.DeploymentExecutionStatus.Stopped)
                            {
                                summary.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Starting;
                            }
                            break;
                        case Models.Dtos.Deployment.DeploymentExecutionStatus.Running:
                            summary.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Running;
                            break;
                        case Models.Dtos.Deployment.DeploymentExecutionStatus.Stopping:
                            if (summary.ExecutionStatus != Models.Dtos.Deployment.DeploymentExecutionStatus.Running)
                            {
                                summary.ExecutionStatus = Models.Dtos.Deployment.DeploymentExecutionStatus.Stopping;
                            }
                            break;
                    }
                }
                var usageKey = $"churros_tenant:{_tenantResolver.AccountId}:app:{app.Id}:resource_usage";
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
                // Usage-as-ratio fields (0..N, where 1 = 100% of the configured limit) and the parsed
                // limit itself (cores / bytes). Both are emitted whenever the size string is parsable —
                // the UI uses pct for thresholds and the limit to label tooltips ("99% of 256 MB").
                if (app.Size is not null)
                {
                    if (!string.IsNullOrWhiteSpace(app.Size.Cpu)
                        && app.Size.Cpu.TryParseCpuToCores(out var cpuLimit)
                        && cpuLimit > 0)
                    {
                        metrics["cpu_limit"] = cpuLimit;
                        if (metrics.TryGetValue("cpu_usage", out var cpuUsage))
                            metrics["cpu_usage_pct"] = cpuUsage / cpuLimit;
                    }
                    if (!string.IsNullOrWhiteSpace(app.Size.Memory)
                        && app.Size.Memory.TryParseMemoryToBytes(out var memLimit)
                        && memLimit > 0)
                    {
                        metrics["memory_limit"] = memLimit;
                        if (metrics.TryGetValue("memory_usage", out var memUsage))
                            metrics["memory_usage_pct"] = memUsage / memLimit;
                    }
                    if (!string.IsNullOrWhiteSpace(app.Size.Gpu)
                        && app.Size.Gpu.TryParseCpuToCores(out var gpuLimit)
                        && gpuLimit > 0)
                    {
                        metrics["gpu_limit"] = gpuLimit;
                        if (metrics.TryGetValue("gpu_usage", out var gpuUsage))
                            metrics["gpu_usage_pct"] = gpuUsage / gpuLimit;
                    }
                    if (!string.IsNullOrWhiteSpace(app.Size.Storage)
                        && app.Size.Storage.TryParseMemoryToBytes(out var storageLimit)
                        && storageLimit > 0)
                    {
                        metrics["storage_limit"] = storageLimit;
                        if (metrics.TryGetValue("storage_usage", out var storageUsage))
                            metrics["storage_usage_pct"] = storageUsage / storageLimit;
                    }
                }
                summary.Metrics = metrics;
                result.Add(summary);
            }

            // TODO: Add statistics from Redis

            return new QueryResult<ApplicationSummary>(result, count);
        }
    }
}
