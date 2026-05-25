namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// Real-time resource totals for the environment header bar.
    /// CPU is in cores, memory and storage in bytes, GPU in count.
    /// Internal shape — consumed only by the in-repo UI. Changing field names
    /// here requires updating <c>apps/ui/src/hooks/data/environments.tsx</c>
    /// (and any downstream UI usage) in the same commit; there is no external
    /// API contract to preserve.
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
        /// <summary>Live usage reported by Kubernetes (sum of per-running-pod metrics).</summary>
        public double Used { get; set; }

        /// <summary>Sum of Size across Running/Starting deployments — what the cluster currently reserves.</summary>
        public double Requested { get; set; }

        /// <summary>Sum of Size across every application (running or stopped) — total configured intent.</summary>
        public double Allocated { get; set; }

        /// <summary>Hard ceiling from environment Definition.Limits; null when no quota configured.</summary>
        public double? Quota { get; set; }
    }
}
