using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher<Usuario> hasher,
    IGeneradorToken generadorToken)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        var usuario = await db.Usuarios
            .Include(u => u.Local)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        // Mismo mensaje para usuario inexistente, inactivo y contraseña incorrecta:
        // no le confirmamos a un atacante qué mails existen en el sistema.
        if (usuario is null || !usuario.Activo)
            throw new CredencialesInvalidasException();

        var resultado = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password ?? string.Empty);
        if (resultado == PasswordVerificationResult.Failed)
            throw new CredencialesInvalidasException();

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // El algoritmo de hasheo se actualizó: se re-hashea con el nuevo formato.
            usuario.PasswordHash = hasher.HashPassword(usuario, request.Password!);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiraUtc) = generadorToken.Generar(usuario);
        return new LoginResponse(token, expiraUtc, Mapear(usuario));
    }

    public async Task<UsuarioDto> ObtenerPerfilAsync(int usuarioId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .Include(u => u.Local)
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct)
            ?? throw new RecursoNoEncontradoException("El usuario del token ya no existe.");

        return Mapear(usuario);
    }

    /// <summary>
    /// Invariante del modelo: un Vendedor necesita local asignado; un Admin no debe tener ninguno.
    /// Se expone para poder testearla y para reusarla cuando exista el ABM de usuarios.
    /// </summary>
    public static void ValidarCoherenciaRolLocal(Usuario usuario)
    {
        if (usuario.Rol == Domain.Enums.Rol.Vendedor && usuario.LocalId is null)
            throw new ReglaNegocioException("Un vendedor tiene que tener un local asignado.");

        if (usuario.Rol == Domain.Enums.Rol.Admin && usuario.LocalId is not null)
            throw new ReglaNegocioException("Un administrador no se asigna a un local: opera todos.");
    }

    private static UsuarioDto Mapear(Usuario u)
        => new(u.Id, u.Email, u.Nombre, u.Rol, u.LocalId, u.Local?.Nombre);
}

/// <summary>Email o contraseña incorrectos. Se traduce a HTTP 401.</summary>
public class CredencialesInvalidasException() : Exception("Email o contraseña incorrectos.");
