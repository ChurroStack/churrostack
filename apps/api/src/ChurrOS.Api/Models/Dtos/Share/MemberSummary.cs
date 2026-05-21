using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Share
{
    public class MemberSummary
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public IdentityType Type { get; private set; }

        public MemberSummary(string name, string displayName, IdentityType type)
        {
            Name = name;
            DisplayName = displayName;
            Type = type;
        }
    }
}
