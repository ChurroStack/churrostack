using ChurrunKubernetes.Domain;
using ChurrunKubernetes.Models.Dtos.Deployment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ChurrunKubernetes.Data.Configuration
{
    public class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
    {
        public virtual void Configure(EntityTypeBuilder<Deployment> builder)
        {
            builder.Property(p => p.Ports)
            .HasColumnType("TEXT")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSettings.Value),
                v => JsonSerializer.Deserialize<PortDefinition[]>(v, JsonSettings.Value)!);

            builder.Property(p => p.Size)
            .HasColumnType("TEXT")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSettings.Value),
                v => JsonSerializer.Deserialize<DeploymentSizeItem>(v, JsonSettings.Value)!);

            builder.HasKey(o => o.Name);
        }
    }
}
