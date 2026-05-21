namespace ChurrOS.Api.Models.Dtos.Deployment
{
    public class DeploymentStatus
    {
        public DateTimeOffset CreatedAt { get; private set; }
        public int Replicas { get; private set; }
        public int Available { get; private set; }
        public DeploymentCondition[] Conditions { get; private set; }
        public IDictionary<string, string> Annotations { get; private set; }

        public DeploymentStatus(DateTimeOffset createdAt, int replicas, int available, DeploymentCondition[] conditions, IDictionary<string, string> annotations)
        {
            CreatedAt = createdAt;
            Replicas = replicas;
            Available = available;
            Conditions = conditions;
            Annotations = annotations;
        }
    }
}
