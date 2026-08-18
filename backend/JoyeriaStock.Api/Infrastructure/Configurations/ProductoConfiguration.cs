using JoyeriaStock.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("productos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(40);

        // Regla 1: SKU único, garantizado por la base y no sólo por el service.
        builder.HasIndex(p => p.Sku).IsUnique();

        builder.Property(p => p.CodigoLinea)
            .HasMaxLength(6);

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(150);

        // Plata siempre decimal con precisión explícita. Nunca double/float.
        builder.Property(p => p.PrecioBase)
            .HasPrecision(18, 2);

        builder.Property(p => p.Activo)
            .HasDefaultValue(true);
    }
}
