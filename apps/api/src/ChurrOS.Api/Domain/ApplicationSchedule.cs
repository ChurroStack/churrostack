using ChurrOS.Api.Models.Dtos.Application;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    public class ApplicationSchedule
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long ApplicationId { get; protected set; }
        public virtual Application? Application { get; protected set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public bool Enabled { get; set; }

        public string Description { get; set; }

        [Required]
        public string CronExpression { get; set; }

        [Required]
        public HttpRequestItem HttpRequest { get; set; }

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

        public ApplicationSchedule(long accountId, long applicationId, string name, bool enabled, string description, string cronExpression, HttpRequestItem httpRequest, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            ApplicationId = applicationId;
            Name = name;
            Enabled = enabled;
            Description = description;
            CronExpression = cronExpression;
            HttpRequest = httpRequest;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
