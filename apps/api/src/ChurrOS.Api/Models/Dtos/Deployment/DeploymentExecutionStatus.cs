using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Deployment
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum DeploymentExecutionStatus
    {
        [EnumMember(Value = "stopped")] Stopped = 0,
        [EnumMember(Value = "starting")] Starting = 1,
        [EnumMember(Value = "running")] Running = 2,
        [EnumMember(Value = "stopping")] Stopping = 3
    }
}
