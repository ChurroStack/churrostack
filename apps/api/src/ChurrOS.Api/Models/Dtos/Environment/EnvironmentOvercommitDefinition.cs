namespace ChurrOS.Api.Models.Dtos.Environment
{
    /// <summary>
    /// Per-environment resource overcommit factors, supplied by the runner config and carried
    /// on <see cref="EnvironmentDefinition"/>. The quota admission check
    /// (<c>EnsureEnvironmentRunningQuota</c>) multiplies the environment quota by these so that
    /// running apps are accounted by the resource <em>request</em> the cluster reserves rather
    /// than the <em>limit</em> they may burst to.
    /// </summary>
    public class EnvironmentOvercommitDefinition
    {
        /// <summary>CPU overcommit factor (throttle-safe → may exceed 1). Treated as 1.0 when null/≤0.</summary>
        public double? Cpu { get; set; }

        /// <summary>Memory request-sum overcommit factor. Treated as 1.0 when null/≤0 (keeps requests schedulable).</summary>
        public double? Memory { get; set; }

        /// <summary>
        /// Memory burst ceiling factor applied to the sum of running memory <em>limits</em>,
        /// bounding worst-case simultaneous burst. Treated as 1.0 when null/≤0 (no memory
        /// overcommit — Σ limits ≤ quota, as before); set e.g. 1.5 to allow ~1.5× burst.
        /// </summary>
        public double? MemoryBurst { get; set; }

        /// <summary>
        /// Normalizes an overcommit factor: a positive value is used as-is, anything else
        /// (null / zero / negative) falls back to <paramref name="fallback"/>. Shared by the
        /// admission check and the totals display so enforcement and the UI never diverge.
        /// </summary>
        public static double Normalize(double? value, double fallback)
            => value is > 0 ? value.Value : fallback;
    }
}
