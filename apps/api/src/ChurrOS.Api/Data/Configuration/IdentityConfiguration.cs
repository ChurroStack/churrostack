using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using ChurrOS.Api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrOS.Api.Data.Configuration
{
    public class IdentityConfiguration : IEntityTypeConfiguration<Identity>
    {
        public virtual void Configure(EntityTypeBuilder<Identity> builder)
        {
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Acl).WithMany().HasForeignKey(o => new { o.AccountId, o.AclId });
            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });
            builder.HasIndex(o => new { o.AccountId, o.Name }).IsUnique();
            builder.HasMany(o => o.MemberOf).WithOne(o => o.Identity).HasForeignKey(o => new { o.AccountId, o.IdentityId });
            builder.Property(p => p.Properties)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSettings.Value),
                    v => JsonSerializer.Deserialize<IdentityProperties>(v, JsonSettings.Value)!);
            builder.ToTable("identity", "cs");
        }
    }
}
