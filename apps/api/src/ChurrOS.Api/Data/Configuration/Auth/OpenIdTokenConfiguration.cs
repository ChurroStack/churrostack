using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdTokenConfiguration : IEntityTypeConfiguration<OpenIdToken>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdToken> builder)
        {
            builder.ToTable("token", "auth");
        }
    }
}
