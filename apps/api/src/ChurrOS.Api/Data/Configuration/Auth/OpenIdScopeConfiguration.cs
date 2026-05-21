using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdScopeConfiguration : IEntityTypeConfiguration<OpenIdScope>
    {
        public virtual void Configure(EntityTypeBuilder<OpenIdScope> builder)
        {
            builder.ToTable("scope", "auth");
        }
    }
}
