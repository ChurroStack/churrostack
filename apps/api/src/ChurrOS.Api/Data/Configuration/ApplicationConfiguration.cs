using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Models.Dtos.Template.Definition;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public virtual void Configure(EntityTypeBuilder<Application> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Acl).WithMany().HasForeignKey(o => new { o.AccountId, o.AclId });

            builder.HasOne(o => o.Environment).WithMany().HasForeignKey(o => new { o.AccountId, o.EnvironmentId });
            builder.HasOne(o => o.Template).WithMany().HasForeignKey(o => new { o.AccountId, o.TemplateId });
            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.Property(p => p.Size)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<SizeRequestItem>(v, JsonSettings.Value)!);

            builder.Property(p => p.Variables)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<ApplicationEnvironmentVariable[]>(v, JsonSettings.Value)!);

            builder.Property(p => p.Parameters)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IDictionary<string, string[]>>(v, JsonSettings.Value)!);

            builder.Property(p => p.Ports)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSettings.Value),
                   v => JsonSerializer.Deserialize<PortDefinition[]>(v, JsonSettings.Value)!);

            builder.Property(p => p.Metadata)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSettings.Value),
                   v => JsonSerializer.Deserialize<JsonElement>(v, JsonSettings.Value)!);

            builder.HasIndex(e => new { e.AccountId, e.Name }).IsUnique();

            builder.HasIndex(e => e.Tags).HasMethod("GIN");

            builder.ToTable("application", "cs");
        }
    }
}
