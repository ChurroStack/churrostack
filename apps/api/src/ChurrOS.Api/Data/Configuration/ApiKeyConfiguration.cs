using ChurrOS.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration
{
    public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
    {
        public virtual void Configure(EntityTypeBuilder<ApiKey> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Identity).WithMany().HasForeignKey(o => new { o.AccountId, o.IdentityId });
            builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.CreatedById });
            builder.HasOne(o => o.ModifiedBy).WithMany().HasForeignKey(o => new { o.AccountId, o.ModifiedById });

            builder.HasIndex(o => new { o.AccountId, o.Value }).IsUnique();

            builder.ToTable("api_key", "cs");
        }
    }
}
