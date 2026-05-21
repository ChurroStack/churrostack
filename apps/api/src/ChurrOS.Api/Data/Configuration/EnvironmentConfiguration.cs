using ChurrOS.Api.Models.Dtos.Environment;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class EnvironmentConfiguration : IEntityTypeConfiguration<Domain.Environment>
    {
        public virtual void Configure(EntityTypeBuilder<Domain.Environment> builder)
        {
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasIndex(o => new { o.AccountId, o.Name }).IsUnique();
            builder.HasIndex(o => o.SshPublicKey);
            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });
            builder.Property(p => p.Definition)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<EnvironmentDefinition>(v, JsonSettings.Value)!);
            builder.Property(p => p.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<JsonElement>(v, JsonSettings.Value)!);
            builder.Property(p => p.Health)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<EnvironmentHealthItem>(v, JsonSettings.Value)!);

            builder.HasIndex(e => e.Tags).HasMethod("GIN");

            builder.ToTable("environment", "cs");
        }
    }
}
