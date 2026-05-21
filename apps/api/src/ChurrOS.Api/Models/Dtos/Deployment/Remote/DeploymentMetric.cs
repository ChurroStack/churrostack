namespace ChurrOS.Api.Models.Dtos.Deployment.Remote
{
    public class DeploymentMetric
    {
        public string Name { get; set; }
        public string AppName { get; set; }
        public string Target { get; set; }
        public double? CpuUsage { get; set; }
        public double? MemoryUsage { get; set; }
        public double? StorageUsage { get; set; }
        public double? GpuUsage { get; set; }

        public DeploymentMetric(string name, string appName, string target, double? cpuUsage, double? memoryUsage, double? storageUsage, double? gpuUsage)
        {
            Name = name;
            AppName = appName;
            Target = target;
            CpuUsage = cpuUsage;
            MemoryUsage = memoryUsage;
            StorageUsage = storageUsage;
            GpuUsage = gpuUsage;
        }
    }
}
