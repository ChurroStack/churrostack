using ChurrOS.Api.Models.Dtos.Application;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    /// <summary>
    /// Latest application-size recommendation, derived from the last few days of
    /// CPU/memory usage. One row per application; recomputed nightly or on demand.
    /// </summary>
    [PrimaryKey(nameof(AccountId), nameof(ApplicationId))]
    public class ApplicationSizeRecommendation
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long ApplicationId { get; protected set; }
        public virtual Application? Application { get; protected set; }

        /// <summary>
        /// Computed optimal size for the application. Null when there is not enough
        /// usage data to make a recommendation.
        /// </summary>
        public SizeRequestItem? RecommendedSize { get; set; }

        /// <summary>Average observed CPU usage, in cores.</summary>
        [Required]
        public double CpuAvg { get; set; }

        /// <summary>Peak observed CPU usage, in cores.</summary>
        [Required]
        public double CpuMax { get; set; }

        /// <summary>95th-percentile observed CPU usage, in cores.</summary>
        [Required]
        public double CpuP95 { get; set; }

        /// <summary>Average observed memory usage, in bytes.</summary>
        [Required]
        public double MemoryAvg { get; set; }

        /// <summary>Peak observed memory usage, in bytes.</summary>
        [Required]
        public double MemoryMax { get; set; }

        /// <summary>95th-percentile observed memory usage, in bytes.</summary>
        [Required]
        public double MemoryP95 { get; set; }

        /// <summary>Number of metric samples the statistics are based on.</summary>
        [Required]
        public int SampleCount { get; set; }

        /// <summary>Size of the analysis window, in days.</summary>
        [Required]
        public int WindowDays { get; set; }

        [Required]
        public DateTimeOffset ComputedAt { get; set; }

        public ApplicationSizeRecommendation(long accountId, long applicationId)
        {
            AccountId = accountId;
            ApplicationId = applicationId;
        }
    }
}
