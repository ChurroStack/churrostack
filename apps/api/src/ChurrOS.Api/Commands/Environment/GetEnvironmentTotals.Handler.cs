using ChurrOS.Api.Commands.Identity;
using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Services;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChurrOS.Api.Commands.Environment
{
    public class GetEnvironmentTotalsHandler : IRequestHandler<GetEnvironmentTotals, ValueTask<EnvironmentTotalsItem>>
    {
        private readonly ChurrosDbContext _context;
        private readonly IMediator _mediator;
        private readonly IConnectionMultiplexer _redis;
        private readonly ITenantResolver _tenantResolver;

        public GetEnvironmentTotalsHandler(
            ChurrosDbContext context,
            IMediator mediator,
            IConnectionMultiplexer redis,
            ITenantResolver tenantResolver)
        {
            _context = context;
            _mediator = mediator;
            _redis = redis;
            _tenantResolver = tenantResolver;
        }

        public async ValueTask<EnvironmentTotalsItem> Handle(GetEnvironmentTotals request, CancellationToken cancellationToken)
        {
            var environment = await _context.Set<Domain.Environment>()
                .AsNoTracking()
                .Select(e => new { e.Id, e.Name, e.AclId, e.Definition })
                .FirstOrDefaultAsync(e => e.Name == request.EnvironmentName, cancellationToken);

            if (environment == null)
                throw new NotFoundException($"Environment with name '{request.EnvironmentName}' was not found.");

            // Totals are non-sensitive; any Read on the environment is enough.
            if (!await _mediator.Send(new IsAdminOrHasAcl(environment.AclId, Permission.Read), cancellationToken))
                throw new UnauthorizedAccessException("You do not have permission to view environment totals.");

            var apps = await _context.Set<Domain.Application>()
                .AsNoTracking()
                .Where(a => a.EnvironmentId == environment.Id)
                .Select(a => new { a.Id, a.Size })
                .ToListAsync(cancellationToken);

            var result = new EnvironmentTotalsItem();

            // Allocated: sum of every app's configured Size (running OR stopped — total configured intent).
            foreach (var app in apps)
            {
                if (app.Size == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(app.Size.Cpu) && app.Size.Cpu.TryParseCpuToCores(out var cpu))
                    result.Cpu.Allocated += cpu;
                if (!string.IsNullOrWhiteSpace(app.Size.Memory) && app.Size.Memory.TryParseMemoryToBytes(out var memory))
                    result.Memory.Allocated += memory;
                if (!string.IsNullOrWhiteSpace(app.Size.Gpu) && app.Size.Gpu.TryParseCpuToCores(out var gpu))
                    result.Gpu.Allocated += gpu;
                if (!string.IsNullOrWhiteSpace(app.Size.Storage) && app.Size.Storage.TryParseMemoryToBytes(out var storage))
                    result.Storage.Allocated += storage;
            }

            // Requested: sum of Size across Running/Starting deployments. Mirrors
            // EnsureEnvironmentRunningQuota — per-deployment, so a Workspace app with N active
            // per-user deployments contributes N × Size (matches what the cluster reserves).
            var runningDeployments = await _context.Set<Domain.ApplicationDeployment>()
                .AsNoTracking()
                .Where(d => d.Application!.EnvironmentId == environment.Id
                         && (d.ExecutionStatus == DeploymentExecutionStatus.Running
                          || d.ExecutionStatus == DeploymentExecutionStatus.Starting))
                .Select(d => new { d.Application!.Size })
                .ToListAsync(cancellationToken);

            foreach (var d in runningDeployments)
            {
                if (d.Size == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(d.Size.Cpu) && d.Size.Cpu.TryParseCpuToCores(out var cpu))
                    result.Cpu.Requested += cpu;
                if (!string.IsNullOrWhiteSpace(d.Size.Memory) && d.Size.Memory.TryParseMemoryToBytes(out var memory))
                    result.Memory.Requested += memory;
                if (!string.IsNullOrWhiteSpace(d.Size.Gpu) && d.Size.Gpu.TryParseCpuToCores(out var gpu))
                    result.Gpu.Requested += gpu;
                if (!string.IsNullOrWhiteSpace(d.Size.Storage) && d.Size.Storage.TryParseMemoryToBytes(out var storage))
                    result.Storage.Requested += storage;
            }

            // Used: sum of the per-app `resource_usage` hashes ScrapeMetricsJob already
            // maintains in Redis. Stopped apps drop out naturally (their hash is missing
            // or expires). HashGetAllAsync calls are pipelined by StackExchange.Redis
            // through the shared multiplexer.
            var db = _redis.GetDatabase();
            var accountId = _tenantResolver.AccountId;
            var hashTasks = apps
                .Select(a => db.HashGetAllAsync($"churros_tenant:{accountId}:app:{a.Id}:resource_usage"))
                .ToArray();
            var hashes = await Task.WhenAll(hashTasks);
            foreach (var entries in hashes)
            {
                if (entries == null)
                    continue;
                foreach (var entry in entries)
                {
                    if (!entry.Value.TryParse(out double value))
                        continue;
                    var name = (string?)entry.Name;
                    switch (name)
                    {
                        case "cpu": result.Cpu.Used += value; break;
                        case "memory": result.Memory.Used += value; break;
                        case "gpu": result.Gpu.Used += value; break;
                        case "storage": result.Storage.Used += value; break;
                    }
                }
            }

            // Quota: env hard ceiling (parsed from the same strings the header already shows).
            var limits = environment.Definition?.Limits;
            if (limits != null)
            {
                result.Cpu.Quota = TryParseCores(limits.Cpu);
                result.Memory.Quota = TryParseBytes(limits.Memory);
                result.Gpu.Quota = TryParseCores(limits.Gpu);
                result.Storage.Quota = TryParseBytes(limits.Storage);
            }

            return result;
        }

        private static double? TryParseCores(string? value)
            => !string.IsNullOrWhiteSpace(value) && value.TryParseCpuToCores(out var cores) ? cores : null;

        private static double? TryParseBytes(string? value)
            => !string.IsNullOrWhiteSpace(value) && value.TryParseMemoryToBytes(out var bytes) ? bytes : null;
    }
}
