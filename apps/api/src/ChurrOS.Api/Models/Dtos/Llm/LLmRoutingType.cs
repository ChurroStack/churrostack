using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum LLmRoutingType : byte
    {
        [EnumMember(Value = "round_robin")] RoundRobin = 0,
    }
}
