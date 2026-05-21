using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum IdentityType
    {
        [EnumMember(Value = "user")] User,
        [EnumMember(Value = "group")] Group,
        [EnumMember(Value = "application")] Application
    }
}
