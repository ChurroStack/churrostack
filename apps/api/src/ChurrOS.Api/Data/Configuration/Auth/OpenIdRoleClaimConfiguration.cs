using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdRoleClaimConfiguration : IEntityTypeConfiguration<OpenIdRoleClaim>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdRoleClaim> builder)
        {
            // Primary key
            builder.HasKey(rc => rc.Id);

            builder.ToTable("role_claim", "auth");
        }
    }
}