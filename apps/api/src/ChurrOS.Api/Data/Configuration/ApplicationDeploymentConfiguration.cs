using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Deployment;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationDeploymentConfiguration : IEntityTypeConfiguration<ApplicationDeployment>
    {
        public virtual void Configure(EntityTypeBuilder<ApplicationDeployment> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.ApplicationId, o.Name });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Application).WithMany(o => o.Deployments).HasForeignKey(o => new { o.AccountId, o.ApplicationId });

            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.Property(p => p.DeploymentStatus)
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonSettings.Value),
                   v => JsonSerializer.Deserialize<DeploymentStatus>(v, JsonSettings.Value)!);

            builder.Property(p => p.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<JsonElement>(v, JsonSettings.Value)!);

            builder.HasIndex(e => new { e.AccountId, e.Name }).IsUnique();

            builder.ToTable("application_deployment", "cs");
        }
    }
}
