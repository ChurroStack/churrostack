using ChurrOS.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration
{
    public class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
    {
        public virtual void Configure(EntityTypeBuilder<MetricValue> builder)
        {
            builder.HasNoKey();

            builder.HasIndex(e => new { e.AccountId, e.MetricId });

            builder.ToTable("metric_value", "cs");
        }
    }
}
