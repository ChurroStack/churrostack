using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum IdentityRole : byte
    {
        [EnumMember(Value = "user")] User = 0,
        [EnumMember(Value = "administrator")] Administrator = 1,
    }
}
