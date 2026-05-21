using ChurrOS.Api.Models.Dtos.Llm;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class Llm
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        [Required]
        public string[] Names { get; set; }

        [Required]
        public long AclId { get; protected set; }
        public virtual Acl? Acl { get; protected set; }

        [Required]
        public LLmRoutingType Routing { get; protected set; }

        [Required]
        public LLmDestinationItem[] Destination { get; set; }

        public LLmDestinationItem? Fallback { get; set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        [Required]
        public long CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; set; }

        [Required]
        public long ModifiedById { get; set; }
        public virtual Identity? ModifiedBy { get; protected set; }

        [Required]
        public IDictionary<string, bool> Capabilities { get; protected set; }

        public Llm(long accountId, long id, string[] names, long aclId, LLmRoutingType routing, LLmDestinationItem[] destination, LLmDestinationItem? fallback, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById, IDictionary<string, bool> capabilities)
        {
            AccountId = accountId;
            Id = id;
            Names = names;
            AclId = aclId;
            Routing = routing;
            Destination = destination;
            Fallback = fallback;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedById = modifiedById;
            ModifiedAt = modifiedAt;
            Capabilities = capabilities;
        }
    }
}
