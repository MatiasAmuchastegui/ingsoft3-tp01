using JoyeriaStock.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");

        // Clave compuesta: imposible tener dos filas para el mismo producto+local.
        builder.HasKey(s => new { s.ProductoId, s.LocalId });

        builder.HasOne(s => s.Producto)
            .WithMany(p => p.Stocks)
            .HasForeignKey(s => s.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Local)
            .WithMany(l => l.Stocks)
            .HasForeignKey(s => s.LocalId)
            .OnDelete(DeleteBehavior.Restrict);

        // Regla 2 garantizada por la BASE, no por el código: aunque un bug, un UPDATE a mano
        // o dos ventas simultáneas se salteen la verificación del service, la cantidad no
        // puede quedar negativa. La transacción que lo intente se cae acá.
        //
        // Es también la respuesta a la concurrencia. Se probó un token optimista sobre la
        // columna de sistema xmin, pero UseXminAsConcurrencyToken() quedó obsoleto en Npgsql 8
        // y configurarlo a mano hace que la migración intente crear una columna llamada "xmin",
        // que PostgreSQL rechaza por ser un nombre de sistema reservado. Este CHECK cubre el
        // mismo caso con menos maquinaria: ver decisiones.md.
        builder.ToTable(t => t.HasCheckConstraint("ck_stocks_cantidad_no_negativa", "cantidad >= 0"));
    }
}
