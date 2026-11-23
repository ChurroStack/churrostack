using ChurrOS.Api.Domain;
using ChurrOS.Api.Models.Dtos.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration
{
    public class IdentityMemberOfConfiguration : IEntityTypeConfiguration<IdentityMemberOf>
    {
        public virtual void Configure(EntityTypeBuilder<IdentityMemberOf> builder)
        {
            // Primary key
            builder.HasKey(o => new { o.AccountId, o.IdentityId, o.GroupId });
            builder.HasOne(o => o.Account).WithMany().HasForeignKey(o => o.AccountId);
            builder.HasOne(o => o.Identity).WithMany(o => o.MemberOf).HasForeignKey(o => new { o.AccountId, o.IdentityId });
            builder.HasOne(o => o.Group).WithMany().HasForeignKey(o => new { o.AccountId, o.GroupId }); ;

            builder.Property(o => o.Source).HasDefaultValue(IdentitySource.Local);

            builder.ToTable("identity_member", "cs");
        }
    }
}
