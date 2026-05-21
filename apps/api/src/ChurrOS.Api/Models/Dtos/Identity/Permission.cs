using ChurrOS.Api.Utils.Converters;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    [Flags]
    [JsonConverter(typeof(NumericFlagsEnumConverter<Permission>))]
    public enum Permission : byte
    {
        None = 0,       // No permissions
        Execute = 1,    // 1 << 0
        Write = 2,      // 1 << 1
        Read = 4,       // 1 << 2
        Manage = 8      // 1 << 3
    }
}
