using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Application;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationScheduleConfiguration : IEntityTypeConfiguration<ApplicationSchedule>
    {
        public virtual void Configure(EntityTypeBuilder<ApplicationSchedule> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.ApplicationId, o.Name });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Application).WithMany().HasForeignKey(o => new { o.AccountId, o.ApplicationId });

            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.Property(p => p.HttpRequest)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSettings.Value),
                   v => JsonSerializer.Deserialize<HttpRequestItem>(v, JsonSettings.Value)!);

            builder.ToTable("application_schedule", "cs");
        }
    }
}
