using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Commands.Identity
{
    public class UpsertIdentity : IRequest<UpsertIdentity, ValueTask<IdentityWithAssignedItem>>
    {
        public class UpsertIdentityBody
        {
            public string? Name { get; set; }

            [Required]
            public string DisplayName { get; private set; }

            [Required]
            public IdentityType Type { get; private set; }

            [Required]
            public IdentityRole Role { get; private set; }

            public string[]? Assigned { get; private set; }

            public UpsertIdentityBody(string? name, string displayName, IdentityType type, IdentityRole role, string[]? assigned = null)
            {
                Name = name;
                DisplayName = displayName;
                Type = type;
                Role = role;
                Assigned = assigned ?? [];
            }
        }

        [Required]
        public UpsertIdentityBody Body { get; private set; }

        public string? IfNoneMatch { get; private set; }

        public UpsertIdentity(UpsertIdentityBody body, string? ifNoneMatch)
        {
            Body = body;
            IfNoneMatch = ifNoneMatch;
        }
    }
}
