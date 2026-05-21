using ChurrOS.Api.Models.Dtos.Identity;

namespace ChurrOS.Api.Models.Dtos.Application
{
    public class ApplicationScheduleItem
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; }
        public string CronExpression { get; set; }
        public HttpRequestItem HttpRequest { get; set; }
        public DateTimeOffset CreatedAt { get; protected set; }
        public IdentitySummary CreatedBy { get; protected set; }
        public DateTimeOffset ModifiedAt { get; protected set; }
        public IdentitySummary ModifiedBy { get; protected set; }

        public ApplicationScheduleItem(string name, bool enabled, string description, string cronExpression, HttpRequestItem httpRequest, DateTimeOffset createdAt, IdentitySummary createdBy, DateTimeOffset modifiedAt, IdentitySummary modifiedBy)
        {
            Name = name;
            Enabled = enabled;
            Description = description;
            CronExpression = cronExpression;
            HttpRequest = httpRequest;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            ModifiedAt = modifiedAt;
            ModifiedBy = modifiedBy;
        }
    }
}
