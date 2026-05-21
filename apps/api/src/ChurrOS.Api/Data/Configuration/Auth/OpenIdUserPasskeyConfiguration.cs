using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdUserPasskeyConfiguration : IEntityTypeConfiguration<OpenIdUserPasskey>
    {
        public void Configure(EntityTypeBuilder<OpenIdUserPasskey> builder)
        {
            builder.HasKey(l => new { l.CredentialId });
            builder.Property(p => p.CredentialId).HasMaxLength(1024);
            builder.OwnsOne(p => p.Data).ToJson();
            builder.ToTable("user_passkey", "auth");
        }
    }
}
