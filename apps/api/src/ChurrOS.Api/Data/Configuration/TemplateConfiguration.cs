using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class TemplateConfiguration : IEntityTypeConfiguration<Template>
    {
        public virtual void Configure(EntityTypeBuilder<Template> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);

            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });
            builder.HasOne(o => o.Category).WithMany(o => o.Templates).HasForeignKey(o => new { o.AccountId, o.CategoryId });

            builder.Property(p => p.Definition)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<TemplateDefinition>(v, JsonSettings.Value)!);

            builder.Property(p => p.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<JsonElement>(v, JsonSettings.Value)!);

            builder.Property(e => e.Name)
                .HasComputedColumnSql("(definition ->> 'name')", stored: true);

            builder.Property(e => e.Target)
                .HasComputedColumnSql("(definition ->> 'target')", stored: true);

            builder.Property(e => e.Title)
                .HasComputedColumnSql("(definition ->> 'title')", stored: true);

            builder.Property(e => e.Description)
                .HasComputedColumnSql("(definition ->> 'description')", stored: true);

            builder.Property(e => e.Icon)
                .HasComputedColumnSql("(definition ->> 'icon')", stored: true);

            builder.Property(e => e.Type)
                .HasComputedColumnSql("(definition ->> 'type')", stored: true);

            builder.HasIndex(e => new { e.AccountId, e.Name, e.Target }).IsUnique();

            builder.ToTable("template", "cs");
        }
    }
}
