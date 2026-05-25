using ChurrOS.Api.Models.Dtos.Application;
using DispatchR.Abstractions.Send;

namespace ChurrOS.Api.Commands.Environment
{
    public enum EnsureRunningQuotaMode
    {
        Start = 0,
        Update = 1,
    }

    /// <summary>
    /// Pre-flight check that the environment's runtime CPU/Memory budget can accommodate the
    /// requested change. Only deployments in <c>Running</c> or <c>Starting</c> state count toward
    /// the total — stopped/stopping deployments are free.
    /// </summary>
    public class EnsureEnvironmentRunningQuota : IRequest<EnsureEnvironmentRunningQuota, Task>
    {
        public long EnvironmentId { get; }
        public long ApplicationId { get; }
        public SizeRequestItem? NewSize { get; }
        public EnsureRunningQuotaMode Mode { get; }

        public EnsureEnvironmentRunningQuota(long environmentId, long applicationId, SizeRequestItem? newSize, EnsureRunningQuotaMode mode)
        {
            EnvironmentId = environmentId;
            ApplicationId = applicationId;
            NewSize = newSize;
            Mode = mode;
        }
    }
}
