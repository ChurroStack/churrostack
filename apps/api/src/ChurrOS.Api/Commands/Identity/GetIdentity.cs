using ChurrOS.Api.Models.Dtos.Identity;
using DispatchR.Abstractions.Send;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Commands.Identity
{
    public class GetIdentity : IRequest<GetIdentity, ValueTask<IdentityWithAssignedItem>>
    {
        [Required]
        public string IdentityName { get; private set; }

        [Required]
        public bool WithAssignedItems { get; private set; }

        public GetIdentity(string identityName, bool withAssignedItems = true)
        {
            IdentityName = identityName;
            WithAssignedItems = withAssignedItems;
        }
    }
}
