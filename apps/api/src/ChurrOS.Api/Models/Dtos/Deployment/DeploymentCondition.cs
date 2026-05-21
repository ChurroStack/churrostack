namespace ChurrOS.Api.Models.Dtos.Deployment
{
    public class DeploymentCondition
    {
        public DateTimeOffset Timestamp { get; private set; }
        public string Type { get; private set; }
        public string Reason { get; private set; }
        public string Message { get; private set; }

        public DeploymentCondition(DateTimeOffset timestamp, string type, string reason, string message)
        {
            Timestamp = timestamp;
            Type = type;
            Reason = reason;
            Message = message;
        }
    }
}
