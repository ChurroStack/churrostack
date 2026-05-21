using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum ProtocolType
    {
        [EnumMember(Value = "generic")] Generic,
        [EnumMember(Value = "web")] Web,
        [EnumMember(Value = "api")] Api,
        [EnumMember(Value = "mcp")] Mcp,
        [EnumMember(Value = "oai")] OpenAI
    }
}
