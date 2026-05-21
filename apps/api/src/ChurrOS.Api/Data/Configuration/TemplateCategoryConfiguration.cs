using ChurrOS.Api.Domain;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class TemplateCategoryConfiguration : IEntityTypeConfiguration<TemplateCategory>
    {
        public virtual void Configure(EntityTypeBuilder<TemplateCategory> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);

            builder.HasIndex(e => new { e.AccountId, e.Name }).IsUnique();

            builder.Property(p => p.Translation)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IDictionary<string, string>>(v, JsonSettings.Value)!);

            builder.ToTable("template_category", "cs");
        }
    }
}
