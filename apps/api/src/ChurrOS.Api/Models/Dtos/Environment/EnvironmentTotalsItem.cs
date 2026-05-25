namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// Real-time resource totals for the environment header bar.
    /// CPU is in cores, memory and storage in bytes, GPU in count.
    /// </summary>
    public class EnvironmentTotalsItem
    {
        public ResourceTotal Cpu { get; set; } = new();
        public ResourceTotal Memory { get; set; } = new();
        public ResourceTotal Gpu { get; set; } = new();
        public ResourceTotal Storage { get; set; } = new();
    }

    public class ResourceTotal
    {
        /// <summary>Observed usage (sum of every running app's latest scraped sample).</summary>
        public double Used { get; set; }

        /// <summary>Reserved/committed capacity (sum of every app's configured Size, running or stopped).</summary>
        public double Requested { get; set; }

        /// <summary>Quota ceiling from the environment definition; null when no limit is configured.</summary>
        public double? Total { get; set; }
    }
}
