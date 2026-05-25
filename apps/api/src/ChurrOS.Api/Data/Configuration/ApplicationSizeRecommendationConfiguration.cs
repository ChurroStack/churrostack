using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationSizeRecommendationConfiguration : IEntityTypeConfiguration<ApplicationSizeRecommendation>
    {
        public void Configure(EntityTypeBuilder<ApplicationSizeRecommendation> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.ApplicationId });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Application).WithMany().HasForeignKey(o => new { o.AccountId, o.ApplicationId });

            // Stored as JSON; EF leaves a null property as a SQL NULL and only runs
            // the converter for non-null values.
            builder.Property(p => p.RecommendedSize)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<SizeRequestItem>(v, JsonSettings.Value)!);

            builder.ToTable("application_size_recommendation", "cs");
        }
    }
}
