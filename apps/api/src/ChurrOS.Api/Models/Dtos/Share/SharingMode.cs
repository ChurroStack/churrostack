using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Share
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum SharingMode
    {
        [EnumMember(Value = "none")] None = 0,
        [EnumMember(Value = "members")] Members = 1,
    }
}
