using ChurrOS.Api.Utils;

namespace ChurrOS.Api.Models.Dtos.Application
{
    /// <summary>
    /// A stored Application Size recommendation together with the usage statistics
    /// it was derived from.
    /// </summary>
    public class ApplicationSizeRecommendationItem
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

        public DateTimeOffset ComputedAt { get; set; }

        /// <summary>True when a different size is recommended and worth surfacing.</summary>
        public bool HasRecommendation { get; set; }

        /// <summary>One of the <see cref="SizeRecommendation"/> direction constants.</summary>
        public string Direction { get; set; } = SizeRecommendation.InsufficientData;
    }
}
