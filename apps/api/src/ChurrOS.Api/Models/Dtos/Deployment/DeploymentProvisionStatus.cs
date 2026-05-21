using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Deployment
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum DeploymentProvisionStatus
    {
        [EnumMember(Value = "pending")] Pending = 0,
        [EnumMember(Value = "provisioning")] Provisioning = 1,
        [EnumMember(Value = "provisioned")] Provisioned = 2,
        [EnumMember(Value = "failed")] Failed = 3
    }
}
