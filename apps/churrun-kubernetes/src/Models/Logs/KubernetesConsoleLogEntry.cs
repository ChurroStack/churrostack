namespace ChurrunKubernetes.Models.Logs
{
    public class KubernetesConsoleLogEntry
    {
        public string PodName { get; private set; }
        public string ContainerName { get; private set; }
        public string Line { get; private set; }

        public KubernetesConsoleLogEntry(string podName, string containerName, string line)
        {
            PodName = podName;
            ContainerName = containerName;
            Line = line;
        }
    }
}
