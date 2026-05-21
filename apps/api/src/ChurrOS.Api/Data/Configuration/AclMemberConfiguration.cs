using ChurrOS.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration
{
    public class AclMemberConfiguration : IEntityTypeConfiguration<AclMember>
    {
        public virtual void Configure(EntityTypeBuilder<AclMember> builder)
        {
            builder.HasKey(o => new { o.AccountId, o.AclId, o.IdentityId });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Acl).WithMany(o => o.Members).HasForeignKey(o => new { o.AccountId, o.AclId });
            builder.HasOne(o => o.Identity).WithMany().HasForeignKey(o => new { o.AccountId, o.IdentityId });
            builder.ToTable("acl_member", "cs");
        }
    }
}
