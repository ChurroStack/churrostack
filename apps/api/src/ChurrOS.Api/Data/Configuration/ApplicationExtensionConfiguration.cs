using ChurrOS.Api.Domain;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationExtensionConfiguration : IEntityTypeConfiguration<ApplicationExtension>
    {
        public virtual void Configure(EntityTypeBuilder<ApplicationExtension> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.ApplicationId, o.Name });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Application).WithMany(o => o.Extensions).HasForeignKey(o => new { o.AccountId, o.ApplicationId });

            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.Property(p => p.Parameters)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IDictionary<string, string[]>>(v, JsonSettings.Value)!);

            builder.ToTable("application_extension", "cs");
        }
    }
}
