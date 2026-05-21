using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class LlmItem
    {
        public string Id { get; protected set; }
        public string[] Names { get; protected set; }
        public LLmDestinationItem[] Destination { get; protected set; }
        public LLmDestinationItem? Fallback { get; protected set; }
        public MemberSummary[] Members { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public LlmItem(string id, string[] names, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy, LLmDestinationItem[] destination, LLmDestinationItem? fallback, MemberSummary[] members)
        {
            Id = id;
            Names = names;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
            Destination = destination;
            Fallback = fallback;
            Members = members;
        }
    }
}
