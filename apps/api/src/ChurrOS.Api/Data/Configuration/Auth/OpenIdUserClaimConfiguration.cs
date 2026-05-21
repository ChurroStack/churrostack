using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdUserClaimConfiguration : IEntityTypeConfiguration<OpenIdUserClaim>
    {
        public void Configure(EntityTypeBuilder<OpenIdUserClaim> builder)
        {
            // Primary key
            builder.HasKey(uc => uc.Id);

            builder.ToTable("user_claim", "auth");
        }
    }
}
