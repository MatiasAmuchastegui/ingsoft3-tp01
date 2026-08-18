using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class MovimientoConfiguration : IEntityTypeConfiguration<Movimiento>
{
    public void Configure(EntityTypeBuilder<Movimiento> builder)
    {
        builder.ToTable("movimientos");

        builder.HasKey(m => m.Id);

        // Los enums se guardan como texto: en psql se lee 'Venta', no '3'.
        // 30 y no 20: "TransferenciaEntrada" mide exactamente 20 caracteres y no entraba.
        builder.Property(m => m.Tipo)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(m => m.Cantidad).IsRequired();

        // timestamptz. Npgsql 6+ exige DateTime con Kind.Utc para este tipo de columna.
        builder.Property(m => m.FechaUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(m => m.Observacion).HasMaxLength(300);

        builder.Property(m => m.PrecioUnitarioAplicado).HasPrecision(18, 2);
        builder.Property(m => m.Total).HasPrecision(18, 2);

        builder.HasOne(m => m.Producto)
            .WithMany()
            .HasForeignKey(m => m.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Local)
            .WithMany()
            .HasForeignKey(m => m.LocalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Usuario)
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // El listado siempre se consulta por local y ordenado por fecha descendente.
        builder.HasIndex(m => new { m.LocalId, m.FechaUtc });
        builder.HasIndex(m => m.ProductoId);

        // Para recuperar los dos asientos de una transferencia de una sola consulta.
        builder.HasIndex(m => m.TransferenciaId);
    }
}
