using JoyeriaStock.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("categorias");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre)
            .IsRequired()
            .HasMaxLength(80);

        builder.HasIndex(c => c.Nombre).IsUnique();

        builder.Property(c => c.PrefijoSku)
            .IsRequired()
            .HasMaxLength(6);

        // Único para que el prefijo diga sin ambigüedad de qué categoría es el producto.
        builder.HasIndex(c => c.PrefijoSku).IsUnique();

        // Regla 3: la base también se niega a borrar una categoría con productos.
        // El service da el mensaje lindo; esto es la red de seguridad si alguien
        // escribe SQL a mano o se agrega otro camino de borrado más adelante.
        builder.HasMany(c => c.Productos)
            .WithOne(p => p.Categoria)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
