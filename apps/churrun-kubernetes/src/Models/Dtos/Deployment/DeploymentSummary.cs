namespace ChurrunKubernetes.Models.Dtos.Deployment
{
    public class DeploymentSummary
    {
        public string Name { get; private set; }
        public byte[] Hash { get; private set; }
        public string Template { get; private set; }
        public DateTimeOffset CreatedOn { get; private set; }

        public DeploymentSummary(string name, byte[] hash, string template, DateTimeOffset createdOn)
        {
            Name = name;
            Hash = hash;
            Template = template;
            CreatedOn = createdOn;
        }
    }
}
