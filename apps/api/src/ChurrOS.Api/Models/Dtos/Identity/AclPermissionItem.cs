using ChurrOS.Api.Utils.Converters;
using System.Text.Json.Serialization;

namespace ChurrOS.Api.Models.Dtos.Identity
{
    public class AclPermissionItem
    {
        public long AclId { get; private set; }

        [JsonConverter(typeof(NumericFlagsEnumConverter<Permission>))]
        public Permission Permission { get; private set; }

        public AclPermissionItem(long aclId, Permission permission)
        {
            AclId = aclId;
            Permission = permission;
        }
    }
}
