using ChurrunKubernetes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrunKubernetes.Data.Configuration
{
    public class TemplateConfiguration : IEntityTypeConfiguration<Template>
    {
        public virtual void Configure(EntityTypeBuilder<Template> builder)
        {
            builder.HasKey(o => o.Id);
            builder.HasIndex(e => new { e.Name, e.Hash }).IsUnique();
        }
    }
}
