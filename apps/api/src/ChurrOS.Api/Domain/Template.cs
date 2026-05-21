using ChurrOS.Api.Models.Dtos.Template.Definition;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class Template
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long CategoryId { get; set; }
        public virtual TemplateCategory? Category { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        public string? Name { get; private set; }
        public string? Target { get; private set; }
        public string? Title { get; private set; }
        public string? Description { get; private set; }
        public string? Icon { get; private set; }
        public string? Type { get; private set; }

        [Required]
        public byte[] Hash { get; set; }

        [Required]
        public TemplateDefinition Definition { get; set; }

        [Required]
        [MaxLength]
        public string Content { get; set; }

        [Required]
        public JsonElement Metadata { get; protected set; }

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

        public Template(long accountId, long id, long categoryId, byte[] hash, TemplateDefinition definition, string content, JsonElement metadata, DateTimeOffset createdAt, long createdById, DateTimeOffset modifiedAt, long modifiedById)
        {
            AccountId = accountId;
            Id = id;
            CategoryId = categoryId;
            Hash = hash;
            Definition = definition;
            Content = content;
            Metadata = metadata;
            CreatedAt = createdAt;
            CreatedById = createdById;
            ModifiedAt = modifiedAt;
            ModifiedById = modifiedById;
        }
    }
}
