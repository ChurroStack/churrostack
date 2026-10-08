namespace ChurrunKubernetes.Models.Dtos.Environment
{
    /// <summary>
    /// Per-environment resource overcommit factors. The control plane multiplies the
    /// environment quota by these when admitting a Start/Deploy, so running apps can be
    /// accounted by what the cluster actually reserves (their request) rather than their
    /// burst ceiling (their limit). Sourced from the runner's <c>Kubernetes:Overcommit:*</c>
    /// config and surfaced on the environment definition.
    /// </summary>
    public class OvercommitDefinition
    {
        /// <summary>CPU overcommit factor (throttle-safe → may be &gt; 1). Default 1.0 when unset.</summary>
        public double? Cpu { get; set; }

        /// <summary>Memory request-sum overcommit factor. Default 1.0 when unset (keeps requests schedulable).</summary>
        public double? Memory { get; set; }

        /// <summary>
        /// Memory burst ceiling factor applied to the sum of running memory *limits*. Bounds the
        /// worst-case simultaneous memory burst (OOM is fatal, so this stays conservative).
        /// Default 1.0 when unset (no memory overcommit); set e.g. 1.5 to allow ~1.5× burst.
        /// </summary>
        public double? MemoryBurst { get; set; }

        public OvercommitDefinition(double? cpu, double? memory, double? memoryBurst)
        {
            Cpu = cpu;
            Memory = memory;
            MemoryBurst = memoryBurst;
        }
    }
}
