using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(ApplicationId), nameof(Name))]
    public class ApplicationExtension
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long EnvironmentId { get; protected set; }
        public virtual Environment? Environment { get; protected set; }

        [Required]
        public long ApplicationId { get; protected set; }
        public virtual Application? Application { get; protected set; }

        [Required]
        public long TemplateId { get; protected set; }
        public virtual Template? Template { get; protected set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; protected set; }

        [Required]
        public bool Enabled { get; set; }

        [Required]
        public IDictionary<string, string[]> Parameters { get; set; }

        [Required]
        public DateTimeOffset CreatedAt { get; protected set; }

        [Required]
        public long CreatedById { get; protected set; }
        public virtual Identity? CreatedBy { get; protected set; }

        [Required]
        public DateTimeOffset ModifiedAt { get; protected set; }

        [Required]
        public long ModifiedById { get; protected set; }
        public virtual Identity? ModifiedBy { get; protected set; }


        public ApplicationExtension(long accountId, long environmentId, long applicationId, long templateId, string name, bool enabled, IDictionary<string, string[]> parameters, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            EnvironmentId = environmentId;
            ApplicationId = applicationId;
            TemplateId = templateId;
            Name = name;
            Enabled = enabled;
            Parameters = parameters;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
