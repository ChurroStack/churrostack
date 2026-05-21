using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum ParameterType
    {
        [EnumMember(Value = "string")] String,
        [EnumMember(Value = "number")] Number,
        [EnumMember(Value = "boolean")] Boolean,
        [EnumMember(Value = "list")] List
    }
}
