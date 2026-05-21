using ChurrunKubernetes.Data;
using ChurrunKubernetes.Utils;
using DispatchR;
using DispatchR.Abstractions.Send;
using Microsoft.EntityFrameworkCore;

namespace ChurrunKubernetes.Commands.Environment
{
    public class EnsureEnvironmentHasQuotaHandler : IRequestHandler<EnsureEnvironmentHasQuota, Task>
    {
        private readonly ChurrunDbContext _dbContext;
        private readonly IMediator _mediator;

        public EnsureEnvironmentHasQuotaHandler(ChurrunDbContext dbContext, IMediator mediator)
        {
            _dbContext = dbContext;
            _mediator = mediator;
        }

        public async Task Handle(EnsureEnvironmentHasQuota request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.Size?.Cpu) ||
                !string.IsNullOrWhiteSpace(request.Size?.Gpu) ||
                !string.IsNullOrWhiteSpace(request.Size?.Memory) ||
                !string.IsNullOrWhiteSpace(request.Size?.Storage))
            {
                var sizes = await _dbContext.Set<Domain.Deployment>().Select(o => o.Size).ToListAsync();
                var envInfo = await _mediator.Send(new GetEnvironment(), cancellationToken);

                if (!string.IsNullOrWhiteSpace(request.Size?.Cpu) && !string.IsNullOrWhiteSpace(envInfo.Limits?.Cpu))
                {
                    var usedCpu = sizes.Select(o =>
                    {
                        if (o?.Cpu?.TryParseCpuToCores(out var cores) ?? false)
                            return cores;
                        return 0;
                    }).Sum();

                    if (envInfo.Limits.Cpu.TryParseCpuToCores(out var limitCpuCores))
                    {
                        var requestCpuCores = 0.0;
                        if (request.Size.Cpu.TryParseCpuToCores(out var reqCores))
                            requestCpuCores = reqCores;
                        if (usedCpu + requestCpuCores > limitCpuCores)
                            throw new InvalidOperationException("The environment CPU quota has been exceeded.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.Size?.Memory) && !string.IsNullOrWhiteSpace(envInfo.Limits?.Memory))
                {
                    var usedMemory = sizes.Select(o =>
                    {
                        if (o?.Memory?.TryParseMemoryToBytes(out var memory) ?? false)
                            return memory;
                        return 0;
                    }).Sum();

                    if (envInfo.Limits.Memory.TryParseMemoryToBytes(out var limitMemoryCores))
                    {
                        var requestMemory = 0.0;
                        if (request.Size.Memory.TryParseMemoryToBytes(out var reqCores))
                            requestMemory = reqCores;
                        if (usedMemory + requestMemory > limitMemoryCores)
                            throw new InvalidOperationException("The environment Memory quota has been exceeded.");
                    }
                }
            }
        }
    }
}
