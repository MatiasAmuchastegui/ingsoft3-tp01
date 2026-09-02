using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

/// <summary>
/// Verifica credenciales y entrega el token de acceso.
/// </summary>
/// <remarks>
/// Las contraseñas nunca se guardan: lo que hay en la base es un hash, y comparar es hashear
/// lo que llegó y ver si coincide. Aunque alguien se lleve la base entera, no se lleva las
/// contraseñas.
/// </remarks>
public class AuthService(
    AppDbContext db,
    IPasswordHasher<Usuario> hasher,
    IGeneradorToken generadorToken)
{
    /// <summary>
    /// Valida email y contraseña y devuelve el token junto con los datos del usuario.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // El email se normaliza para que "Ana@Joyeria.local" y " ana@joyeria.local " entren
        // igual: nadie debería quedar afuera por haber escrito una mayúscula.
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        var usuario = await db.Usuarios
            .Include(u => u.Local)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

        // Mismo mensaje para usuario inexistente, inactivo y contraseña incorrecta:
        // no le confirmamos a un atacante qué mails existen en el sistema.
        if (usuario is null || !usuario.Activo)
            throw new CredencialesInvalidasException();

        // VerifyHashedPassword vuelve a hashear lo que llegó y lo compara con lo guardado.
        // La comparación la hace en tiempo constante, para no filtrar información por cuánto
        // tarda en responder.
        var resultado = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password ?? string.Empty);
        if (resultado == PasswordVerificationResult.Failed)
            throw new CredencialesInvalidasException();

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // La contraseña es correcta pero está guardada con un formato viejo. Este es el
            // único momento en que se tiene la contraseña en claro, así que es el único
            // momento posible para re-hashearla: se aprovecha y se actualiza en silencio.
            usuario.PasswordHash = hasher.HashPassword(usuario, request.Password!);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiraUtc) = generadorToken.Generar(usuario);
        return new LoginResponse(token, expiraUtc, Mapear(usuario));
    }

    /// <summary>
    /// Devuelve el estado ACTUAL del usuario del token.
    /// </summary>
    /// <remarks>
    /// El frontend llama a esto al recargar la página en lugar de leer los datos del propio
    /// token, porque el token pudo firmarse hace horas: el rol o el local pueden haber
    /// cambiado desde entonces. Acá se responde con lo que dice la base hoy.
    ///
    /// Que el usuario ya no exista es posible y no es un error del sistema: el token sigue
    /// siendo válido —está bien firmado y no venció— pero apunta a alguien que se borró.
    /// </remarks>
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
