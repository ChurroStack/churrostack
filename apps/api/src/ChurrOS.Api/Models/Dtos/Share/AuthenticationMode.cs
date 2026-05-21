using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Share
{
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum AuthenticationMode
    {
        [EnumMember(Value = "anonymous")] Anonymous = 0,
        [EnumMember(Value = "jwt")] Jwt = 1,
        [EnumMember(Value = "oidc")] Oidc = 2,
        [EnumMember(Value = "jwt_dcr")] JwtDcr = 3,
    }
}
