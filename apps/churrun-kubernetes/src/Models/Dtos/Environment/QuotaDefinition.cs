namespace ChurrunKubernetes.Models.Dtos.Environment
{
    public class QuotaDefinition
    {
        /// <summary>
        /// Cpu quota (e.g. "4", "500m")
        /// </summary>
        public string? Cpu { get; set; }

        /// <summary>
        /// Memory quota (e.g. "8Gi", "512Mi")
        /// </summary>
        public string? Memory { get; set; }

        /// <summary>
        /// Gpu quota (e.g. "1", "nvidia.com/gpu:1")
        /// </summary>
        public string? Gpu { get; set; }

        /// <summary>
        /// Storage quota (e.g. "100Gi", "1Ti")
        /// </summary>
        public string? Storage { get; set; }

        public QuotaDefinition(string? cpu, string? memory, string? gpu, string? storage)
        {
            Cpu = cpu;
            Memory = memory;
            Gpu = gpu;
            Storage = storage;
        }
    }
}
