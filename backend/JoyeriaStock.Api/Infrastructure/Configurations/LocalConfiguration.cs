using JoyeriaStock.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class LocalConfiguration : IEntityTypeConfiguration<Local>
{
    public void Configure(EntityTypeBuilder<Local> builder)
    {
        builder.ToTable("locales");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Direccion)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(l => l.Nombre).IsUnique();
    }
}
