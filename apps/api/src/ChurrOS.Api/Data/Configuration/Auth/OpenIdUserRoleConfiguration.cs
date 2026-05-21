using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdUserRoleConfiguration : IEntityTypeConfiguration<OpenIdUserRole>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdUserRole> builder)
        {
            // Primary key
            builder.HasKey(r => new { r.UserId, r.RoleId });

            builder.ToTable("user_role", "auth");
        }
    }
}