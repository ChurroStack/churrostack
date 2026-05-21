namespace ChurrunKubernetes.Models.Dtos.Deployment
{
    public class DeploymentSizeItem
    {
        /// <summary>
        /// Size hint
        /// </summary>
        public string? Hint { get; private set; }

        /// <summary>
        /// Cpu quota (e.g. "4", "500m")
        /// </summary>
        public string? Cpu { get; private set; }

        /// <summary>
        /// Memory quota (e.g. "8Gi", "512Mi")
        /// </summary>
        public string? Memory { get; private set; }

        /// <summary>
        /// Gpu quota (e.g. "1", "nvidia.com/gpu:1")
        /// </summary>
        public string? Gpu { get; private set; }

        /// <summary>
        /// Storage quota (e.g. "100Gi", "1Ti")
        /// </summary>
        public string? Storage { get; private set; }

        public DeploymentSizeItem(string? hint, string? cpu, string? memory, string? gpu, string? storage)
        {
            Hint = hint;
            Cpu = cpu;
            Memory = memory;
            Gpu = gpu;
            Storage = storage;
        }
    }
}
