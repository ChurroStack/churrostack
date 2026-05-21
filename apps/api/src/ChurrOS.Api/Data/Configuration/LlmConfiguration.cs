using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Llm;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class LlmConfiguration : IEntityTypeConfiguration<Llm>
    {
        public virtual void Configure(EntityTypeBuilder<Llm> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Acl).WithMany().HasForeignKey(o => new { o.AccountId, o.AclId });
            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.Property(p => p.Destination)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<LLmDestinationItem[]>(v, JsonSettings.Value)!);

            builder.Property(p => p.Fallback)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<LLmDestinationItem>(v, JsonSettings.Value)!);

            builder.Property(p => p.Capabilities)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IDictionary<string, bool>>(v, JsonSettings.Value)!);

            builder.HasIndex(e => new { e.AccountId, e.Names }).IsUnique();

            builder.ToTable("llm", "cs");
        }
    }
}
