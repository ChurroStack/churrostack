using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdApplicationConfiguration : IEntityTypeConfiguration<OpenIdApplication>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdApplication> builder)
        {
            builder.ToTable("application", "auth");
        }
    }
}
