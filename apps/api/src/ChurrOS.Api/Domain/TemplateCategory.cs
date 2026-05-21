using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ChurrOS.Api.Domain
{
    [PrimaryKey(nameof(AccountId), nameof(Id))]
    public class TemplateCategory
    {
        [Required]
        public long AccountId { get; protected set; }
        public virtual Account? Account { get; protected set; }

        [Required]
        public long Id { get; protected set; }

        [MaxLength(255)]
        [Required]
        public string Name { get; protected set; }

        [MaxLength(4095)]
        [Required]
        public string Title { get; protected set; }

        [MaxLength(4095)]
        [Required]
        public string Icon { get; protected set; }

        public IDictionary<string, string>? Translation { get; set; }

        public virtual ICollection<Template>? Templates { get; protected set; }

        public TemplateCategory(long accountId, long id, string name, string title, string icon, IDictionary<string, string>? translation)
        {
            AccountId = accountId;
            Id = id;
            Name = name;
            Title = title;
            Icon = icon;
            Translation = translation;
        }
    }
}
