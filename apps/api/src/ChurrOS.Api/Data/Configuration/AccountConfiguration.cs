using ChurrOS.Api.Domain;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class AccountConfiguration : IEntityTypeConfiguration<Account>
    {
        public virtual void Configure(EntityTypeBuilder<Account> builder)
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();
            builder.Property(p => p.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(o => JsonSerializer.Serialize(o, JsonSettings.Value), o => JsonSerializer.Deserialize<IDictionary<string, object>>(o, JsonSettings.Value));
            builder.HasIndex(o => o.Domains).HasMethod("GIN");
            builder.ToTable("account", "cs");
        }
    }
}
