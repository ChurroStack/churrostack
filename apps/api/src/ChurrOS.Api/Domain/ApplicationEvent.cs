using ChurrOS.Api.Utils;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    public class ApplicationEvent
    {
        [HypertablePartitionColumn]
        public long AccountId { get; set; }
        public long ApplicationId { get; set; }
        public long EnvironmentId { get; set; }
        public string DeploymentName { get; set; }

        [HypertableColumn]
        public DateTimeOffset Timestamp { get; set; }

        public string Target { get; set; }
        public string Type { get; set; }
        public string Reason { get; set; }
        public string Message { get; set; }
        public JsonElement? Tags { get; set; }

        public ApplicationEvent(long accountId, long environmentId, long applicationId, string deploymentName, DateTimeOffset timestamp, string target, string type, string reason, string message, JsonElement? tags)
        {
            AccountId = accountId;
            EnvironmentId = environmentId;
            ApplicationId = applicationId;
            DeploymentName = deploymentName;
            Timestamp = timestamp;
            Target = target;
            Type = type;
            Reason = reason;
            Message = message;
            Tags = tags;
        }
    }
}
