using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Utils;

namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// Per-application usage row shown in the environment Usage tab.
    /// </summary>
    public class EnvironmentUsageItem
    {
        public string ApplicationName { get; set; } = string.Empty;

        public SizeRequestItem? CurrentSize { get; set; }

        public SizeRequestItem? RecommendedSize { get; set; }

        /// <summary>Average observed CPU usage, in cores.</summary>
        public double CpuAvg { get; set; }

        /// <summary>Peak observed CPU usage, in cores.</summary>
        public double CpuMax { get; set; }

        /// <summary>95th-percentile observed CPU usage, in cores.</summary>
        public double CpuP95 { get; set; }

        /// <summary>Average observed memory usage, in bytes.</summary>
        public double MemoryAvg { get; set; }

        /// <summary>Peak observed memory usage, in bytes.</summary>
        public double MemoryMax { get; set; }

        /// <summary>95th-percentile observed memory usage, in bytes.</summary>
        public double MemoryP95 { get; set; }

        public int SampleCount { get; set; }

        public int WindowDays { get; set; }

        /// <summary>When the analysis last ran; null when the app was never analyzed.</summary>
        public DateTimeOffset? ComputedAt { get; set; }

        /// <summary>True when a different size is recommended and worth surfacing.</summary>
        public bool HasRecommendation { get; set; }

        /// <summary>One of the <see cref="SizeRecommendation"/> direction constants.</summary>
        public string Direction { get; set; } = SizeRecommendation.NotAnalyzed;
    }
}
