using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Template.Definition
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum TemplateType
    {
        [EnumMember(Value = "application")] Application,
        [EnumMember(Value = "extension")] Extension
    }
}