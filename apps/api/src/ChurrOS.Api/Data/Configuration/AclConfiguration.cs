using ChurrOS.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration
{
    public class AclConfiguration : IEntityTypeConfiguration<Acl>
    {
        public virtual void Configure(EntityTypeBuilder<Acl> builder)
        {
            builder.HasKey(o => new { o.AccountId, o.Id });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasMany(o => o.Members).WithOne(o => o.Acl).HasForeignKey(o => new { o.AccountId, o.AclId });
            builder.ToTable("acl", "cs");
        }
    }
}
