using JoyeriaStock.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JoyeriaStock.Api.Infrastructure.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Nombre)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(u => u.Rol)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.Activo).HasDefaultValue(true);

        builder.HasOne(u => u.Local)
            .WithMany()
            .HasForeignKey(u => u.LocalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
