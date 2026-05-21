using ChurrOS.Api.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChurrOS.Api.Data.Configuration.Auth
{
    public class OpenIdRoleConfiguration : IEntityTypeConfiguration<OpenIdRole>
    {

        public void Configure(EntityTypeBuilder<OpenIdRole> builder)
        { // Primary key
            builder.HasKey(r => r.Id);

            // Index for "normalized" role name to allow efficient lookups
            builder.HasIndex(r => r.NormalizedName!).HasDatabaseName("role_name_index").IsUnique();

            // Maps to the AspNetRoles table
            builder.ToTable("AspNetRoles");

            // A concurrency token for use with the optimistic concurrency checking
            builder.Property(r => r.ConcurrencyStamp).IsConcurrencyToken();

            // Limit the size of columns to use efficient database types
            builder.Property(u => u.Name).HasMaxLength(256);
            builder.Property(u => u.NormalizedName).HasMaxLength(256);

            // The relationships between Role and other entity types
            // Note that these relationships are configured with no navigation properties

            // Each Role can have many entries in the UserRole join table
            builder.HasMany<OpenIdUserRole>().WithOne().HasForeignKey(ur => ur.RoleId).IsRequired();

            // Each Role can have many associated RoleClaims
            builder.HasMany<OpenIdUserRole>().WithOne().HasForeignKey(rc => rc.RoleId).IsRequired();

            builder.ToTable("role", "auth");
        }
    }
}
