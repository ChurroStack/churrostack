using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Application
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum ApplicationMode
    {
        [EnumMember(Value = "application")] Application = 0,
        [EnumMember(Value = "workspace")] Workspace = 1
    }
}
