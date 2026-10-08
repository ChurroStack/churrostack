using ChurrOS.Api.Data;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Utils;
using ChurrOS.Api.Utils.Exceptions;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChurrOS.Api.Commands.Environment
{
    public class EnsureEnvironmentRunningQuotaHandler : IRequestHandler<EnsureEnvironmentRunningQuota, Task>
    {
        // Neutral default for the memory burst ceiling when the operator configures nothing:
        // Σ memory limits ≤ quota, i.e. memory behaves exactly as before (no oversubscription).
        // Operators enable memory overcommit explicitly (e.g. 1.5) via Kubernetes:Overcommit:MemoryBurst.
        private const double DefaultMemoryBurstFactor = 1.0;

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
            var sizes = environment?.Definition?.Sizes;
            var cpuLimitSet = !string.IsNullOrWhiteSpace(limits?.Cpu) && limits!.Cpu.TryParseCpuToCores(out _);
            var memoryLimitSet = !string.IsNullOrWhiteSpace(limits?.Memory) && limits!.Memory.TryParseMemoryToBytes(out _);
            if (!cpuLimitSet && !memoryLimitSet)
            {
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} no limits configured, skip", request.EnvironmentId);
                return;
            }

            // Overcommit factors (default to neutral/conservative when unset). CPU is throttle-safe
            // so it may be raised aggressively; memory requests stay schedulable (factor ~1.0) while
            // a separate burst ceiling bounds the worst-case simultaneous memory burst.
            var overcommit = environment?.Definition?.Overcommit;
            var fCpu = EnvironmentOvercommitDefinition.Normalize(overcommit?.Cpu, 1.0);
            var fMem = EnvironmentOvercommitDefinition.Normalize(overcommit?.Memory, 1.0);
            var fMemBurst = EnvironmentOvercommitDefinition.Normalize(overcommit?.MemoryBurst, DefaultMemoryBurstFactor);

            // Pull every Running/Starting deployment in this environment. Each deployment counts
            // once: a Workspace app with N active per-user deployments contributes N × request
            // (matches what the cluster actually reserves).
            var runningDeployments = await _context.Set<Domain.ApplicationDeployment>()
                .AsNoTracking()
                .Where(d => d.Application!.EnvironmentId == request.EnvironmentId
                         && (d.ExecutionStatus == DeploymentExecutionStatus.Running
                          || d.ExecutionStatus == DeploymentExecutionStatus.Starting))
                .Select(d => new { d.ApplicationId, d.Application!.Size })
                .ToListAsync(cancellationToken);

            // Running totals accounted by request (cluster reservation); memory also tracks the
            // sum of limits for the burst ceiling.
            double sumCpuReq = 0, sumMemReq = 0, sumMemLimit = 0;
            double candidateCpuReq = 0, candidateMemReq = 0, candidateMemLimit = 0;
            int candidateRunningCount = 0;
            foreach (var d in runningDeployments)
            {
                var (cpuReq, memReq, memLimit) = ResolveContribution(sizes, d.Size, request.EnvironmentId, d.ApplicationId);
                sumCpuReq += cpuReq;
                sumMemReq += memReq;
                sumMemLimit += memLimit;
                if (d.ApplicationId == request.ApplicationId)
                {
                    candidateCpuReq += cpuReq;
                    candidateMemReq += memReq;
                    candidateMemLimit += memLimit;
                    candidateRunningCount++;
                }
            }

            // Delta this request adds. Start adds one instance; Update re-prices every running
            // instance of the candidate (newValue × count − currentCandidateSum).
            var (newCpuReq, newMemReq, newMemLimit) = ResolveContribution(sizes, request.NewSize, request.EnvironmentId, request.ApplicationId);
            double addCpuReq, addMemReq, addMemLimit;
            if (request.Mode == EnsureRunningQuotaMode.Update)
            {
                addCpuReq = newCpuReq * candidateRunningCount - candidateCpuReq;
                addMemReq = newMemReq * candidateRunningCount - candidateMemReq;
                addMemLimit = newMemLimit * candidateRunningCount - candidateMemLimit;
            }
            else
            {
                addCpuReq = newCpuReq;
                addMemReq = newMemReq;
                addMemLimit = newMemLimit;
            }

            if (cpuLimitSet && limits!.Cpu!.TryParseCpuToCores(out var cpuLimit))
            {
                var cpuCeiling = cpuLimit * fCpu;
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} appId={AppId} mode={Mode} cpuReq={Used} cpuAdd={Add} cpuCeiling={Ceiling} (quota={Quota} ×{Factor})",
                    request.EnvironmentId, request.ApplicationId, request.Mode, sumCpuReq, addCpuReq, cpuCeiling, cpuLimit, fCpu);
                if (sumCpuReq + addCpuReq > cpuCeiling)
                {
                    _logger.LogInformation("[EnsureRunningQuota] CPU rejected envId={EnvId} appId={AppId} mode={Mode} reqSum={Used} add={Add} ceiling={Ceiling}",
                        request.EnvironmentId, request.ApplicationId, request.Mode, sumCpuReq, addCpuReq, cpuCeiling);
                    throw new EnvironmentCapacityException("The environment CPU quota has been exceeded.");
                }
            }

            if (memoryLimitSet && limits!.Memory!.TryParseMemoryToBytes(out var memoryLimit))
            {
                var memRequestCeiling = memoryLimit * fMem;
                var memBurstCeiling = memoryLimit * fMemBurst;
                _logger.LogDebug("[EnsureRunningQuota] envId={EnvId} appId={AppId} mode={Mode} memReq={UsedReq} memReqAdd={AddReq} memReqCeiling={ReqCeiling} memLimit={UsedLim} memLimitAdd={AddLim} memBurstCeiling={BurstCeiling}",
                    request.EnvironmentId, request.ApplicationId, request.Mode, sumMemReq, addMemReq, memRequestCeiling, sumMemLimit, addMemLimit, memBurstCeiling);
                if (sumMemReq + addMemReq > memRequestCeiling)
                {
                    _logger.LogInformation("[EnsureRunningQuota] Memory (request) rejected envId={EnvId} appId={AppId} mode={Mode} reqSum={Used} add={Add} ceiling={Ceiling}",
                        request.EnvironmentId, request.ApplicationId, request.Mode, sumMemReq, addMemReq, memRequestCeiling);
                    throw new EnvironmentCapacityException("The environment Memory quota has been exceeded.");
                }
                if (sumMemLimit + addMemLimit > memBurstCeiling)
                {
                    _logger.LogInformation("[EnsureRunningQuota] Memory (burst) rejected envId={EnvId} appId={AppId} mode={Mode} limitSum={Used} add={Add} burstCeiling={Ceiling}",
                        request.EnvironmentId, request.ApplicationId, request.Mode, sumMemLimit, addMemLimit, memBurstCeiling);
                    throw new EnvironmentCapacityException("The environment Memory quota has been exceeded.");
                }
            }
        }

        /// <summary>
        /// Returns the (cpuRequestCores, memoryRequestBytes, memoryLimitBytes) a size contributes.
        /// Requests are what the cluster reserves (used for admission + scheduling fit); the memory
        /// limit feeds the burst ceiling. <see cref="SizeRequestItem.Memory"/> already holds the
        /// preset limit, so it is the memory limit directly.
        /// </summary>
        private (double CpuRequest, double MemoryRequest, double MemoryLimit) ResolveContribution(
            EnvironmentSizeDefinition[]? sizes, SizeRequestItem? size, long environmentId, long applicationId)
        {
            if (size is null)
                return (0, 0, 0);

            var (cpuReq, memReq, fromCatalog) = SizeRecommendation.ResolveRequestDetailed(sizes, size);
            if (!fromCatalog)
            {
                // No catalog request matched (deleted/renamed size, or a preset with no explicit
                // request) — we charge the limit instead. This is the catalog-drift signal.
                _logger.LogDebug("[EnsureRunningQuota] request fallback-to-limit envId={EnvId} appId={AppId} hint={Hint} cpu={Cpu} mem={Mem}",
                    environmentId, applicationId, size.Hint, size.Cpu, size.Memory);
            }
            double memLimit = !string.IsNullOrWhiteSpace(size.Memory) && size.Memory.TryParseMemoryToBytes(out var ml)
                ? ml
                : memReq ?? 0;
            return (cpuReq ?? 0, memReq ?? 0, memLimit);
        }
    }
}
