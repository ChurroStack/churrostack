using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Llm
{
    public class LlmSummary
    {
        public string Id { get; protected set; }
        public string[] Names { get; protected set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public LlmSummary(string id, string[] names, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy)
        {
            Id = id;
            Names = names;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
        }
    }
}
