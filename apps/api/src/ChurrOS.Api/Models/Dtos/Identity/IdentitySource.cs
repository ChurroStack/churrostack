using ChurrOS.Api.Utils.Converters;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    /// <summary>
    /// Origin of an identity or a group membership. <see cref="Local"/> rows are managed by admins
    /// (UI / import) and are never removed by the Entra sync; <see cref="Entra"/> rows are owned by it.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum IdentitySource : byte
    {
        [EnumMember(Value = "local")] Local = 0,
        [EnumMember(Value = "entra")] Entra = 1
    }
}
