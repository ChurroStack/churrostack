using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Utils;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChurrOS.Api.Commands.Environment
{
    public class EnsureEnvironmentRunningQuotaHandler : IRequestHandler<EnsureEnvironmentRunningQuota, Task>
    {
        private readonly ChurrosDbContext _context;
        private readonly ILogger<EnsureEnvironmentRunningQuotaHandler> _logger;

        public EnsureEnvironmentRunningQuotaHandler(ChurrosDbContext context, ILogger<EnsureEnvironmentRunningQuotaHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Handle(EnsureEnvironmentRunningQuota request, CancellationToken cancellationToken)
        {
            var environment = await _context.Set<Domain.Environment>()
                .AsNoTracking()
                .Where(e => e.Id == request.EnvironmentId)
                .Select(e => new { e.Id, e.Definition })
                .FirstOrDefaultAsync(cancellationToken);

            var limits = environment?.Definition?.Limits;
            var cpuLimitSet = !string.IsNullOrWhiteSpace(limits?.Cpu) && limits!.Cpu.TryParseCpuToCores(out _);
            var memoryLimitSet = !string.IsNullOrWhiteSpace(limits?.Memory) && limits!.Memory.TryParseMemoryToBytes(out _);
            if (!cpuLimitSet && !memoryLimitSet)
            {
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} no limits configured, skip", request.EnvironmentId);
                return;
            }

            // Pull every Running/Starting deployment in this environment so we can sum their
            // committed Application.Size. Each deployment counts once: a Workspace app with N
            // active per-user deployments contributes N × Size (matches what the cluster requests).
            var runningDeployments = await _context.Set<Domain.ApplicationDeployment>()
                .AsNoTracking()
                .Where(d => d.Application!.EnvironmentId == request.EnvironmentId
                         && (d.ExecutionStatus == DeploymentExecutionStatus.Running
                          || d.ExecutionStatus == DeploymentExecutionStatus.Starting))
                .Select(d => new { d.ApplicationId, d.Application!.Size })
                .ToListAsync(cancellationToken);

            double usedCpu = 0;
            double usedMemory = 0;
            int candidateRunningCount = 0;
            foreach (var d in runningDeployments)
            {
                if (d.Size is null)
                    continue;
                if (cpuLimitSet && !string.IsNullOrWhiteSpace(d.Size.Cpu) && d.Size.Cpu.TryParseCpuToCores(out var cpu))
                    usedCpu += cpu;
                if (memoryLimitSet && !string.IsNullOrWhiteSpace(d.Size.Memory) && d.Size.Memory.TryParseMemoryToBytes(out var mem))
                    usedMemory += mem;
                if (d.ApplicationId == request.ApplicationId)
                    candidateRunningCount++;
            }

            // Compute the delta this request would add to the running totals.
            double addCpu = 0;
            double addMemory = 0;
            if (cpuLimitSet && !string.IsNullOrWhiteSpace(request.NewSize?.Cpu) && request.NewSize.Cpu.TryParseCpuToCores(out var newCpu))
            {
                addCpu = request.Mode == EnsureRunningQuotaMode.Update
                    ? newCpu * candidateRunningCount - SumCandidateCpu(runningDeployments.Where(o => o.ApplicationId == request.ApplicationId).Select(o => o.Size))
                    : newCpu;
            }
            if (memoryLimitSet && !string.IsNullOrWhiteSpace(request.NewSize?.Memory) && request.NewSize.Memory.TryParseMemoryToBytes(out var newMem))
            {
                addMemory = request.Mode == EnsureRunningQuotaMode.Update
                    ? newMem * candidateRunningCount - SumCandidateMemory(runningDeployments.Where(o => o.ApplicationId == request.ApplicationId).Select(o => o.Size))
                    : newMem;
            }

            if (cpuLimitSet && limits!.Cpu!.TryParseCpuToCores(out var cpuLimit))
            {
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} appId={AppId} mode={Mode} cpuUsed={Used} cpuAdd={Add} cpuLimit={Limit}",
                    request.EnvironmentId, request.ApplicationId, request.Mode, usedCpu, addCpu, cpuLimit);
                if (usedCpu + addCpu > cpuLimit)
                    throw new InvalidOperationException("The environment CPU quota has been exceeded.");
            }
            if (memoryLimitSet && limits!.Memory!.TryParseMemoryToBytes(out var memoryLimit))
            {
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} appId={AppId} mode={Mode} memUsed={Used} memAdd={Add} memLimit={Limit}",
                    request.EnvironmentId, request.ApplicationId, request.Mode, usedMemory, addMemory, memoryLimit);
                if (usedMemory + addMemory > memoryLimit)
                    throw new InvalidOperationException("The environment Memory quota has been exceeded.");
            }
        }

        private static double SumCandidateCpu(IEnumerable<Models.Dtos.Application.SizeRequestItem?> sizes)
        {
            double total = 0;
            foreach (var size in sizes)
            {
                if (!string.IsNullOrWhiteSpace(size?.Cpu) && size!.Cpu.TryParseCpuToCores(out var cpu))
                    total += cpu;
            }
            return total;
        }

        private static double SumCandidateMemory(IEnumerable<Models.Dtos.Application.SizeRequestItem?> sizes)
        {
            double total = 0;
            foreach (var size in sizes)
            {
                if (!string.IsNullOrWhiteSpace(size?.Memory) && size!.Memory.TryParseMemoryToBytes(out var mem))
                    total += mem;
            }
            return total;
        }
    }
}
