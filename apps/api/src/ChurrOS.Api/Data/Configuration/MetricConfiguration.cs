using ChurrOS.Api.Domain;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class MetricConfiguration : IEntityTypeConfiguration<Metric>
    {
        public virtual void Configure(EntityTypeBuilder<Metric> builder)
        {
            builder.HasKey(e => new { e.AccountId, e.Hash });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);

            builder.Property(p => p.Labels)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IDictionary<string, string>>(v, JsonSettings.Value)!);

            builder.ToTable("metric", "cs");
        }
    }
}
