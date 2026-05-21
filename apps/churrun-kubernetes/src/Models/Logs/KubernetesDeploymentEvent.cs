namespace ChurrunKubernetes.Models.Logs
{
    public class KubernetesDeploymentEvent
    {
        public DateTimeOffset Creation { get; private set; }
        public string Name { get; private set; }
        public int Replicas { get; private set; }
        public int Available { get; private set; }
        public KubernetesDeploymentCondition[] Conditions { get; private set; }
        public IDictionary<string, string> Annotations { get; private set; }

        public KubernetesDeploymentEvent(DateTimeOffset creation, string name, int replicas, int available, KubernetesDeploymentCondition[] conditions, IDictionary<string, string> annotations)
        {
            Creation = creation;
            Name = name;
            Replicas = replicas;
            Available = available;
            Conditions = conditions;
            Annotations = annotations;
        }
    }

}
