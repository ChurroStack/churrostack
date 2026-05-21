using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum LLmDestinationType : byte
    {
        [EnumMember(Value = "oai")] OpenAI = 0
    }
}
