using ChurrOS.Api.Domain;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
    {
        public virtual void Configure(EntityTypeBuilder<ApplicationEvent> builder)
        {
            builder.HasNoKey();

            builder.HasIndex(e => new { e.AccountId, e.ApplicationId, e.Target });
            builder.HasIndex(e => new { e.AccountId, e.EnvironmentId, e.Target });

            builder.Property(p => p.Tags)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<JsonElement>(v, JsonSettings.Value)!);

            builder.ToTable("application_event", "cs");
        }
    }
}
