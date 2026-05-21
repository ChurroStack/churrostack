using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdAuthorizationConfiguration : IEntityTypeConfiguration<OpenIdAuthorization>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdAuthorization> builder)
        {
            builder.ToTable("authorization", "auth");
        }
    }
}
