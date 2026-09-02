using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JoyeriaStock.Api.Infrastructure.Auth;

/// <summary>
/// Fabrica el token que identifica a un usuario en cada llamada a la API.
/// </summary>
/// <remarks>
/// La API no guarda sesiones: no hay nada en memoria del servidor que recuerde quién entró.
/// Toda la información de identidad viaja adentro del token, y el servidor confía en ella
/// porque puede verificar que la firmó él mismo. Esa es la razón por la que el sistema podría
/// correr en varias instancias sin compartir estado.
///
/// Recibe <see cref="TimeProvider"/> en lugar de usar <c>DateTime.UtcNow</c> para que en un
/// test se pueda inyectar un reloj falso y comprobar el vencimiento sin esperar de verdad.
/// </remarks>
public class GeneradorTokenJwt(IOptions<JwtOptions> opciones, TimeProvider reloj) : IGeneradorToken
{
    private readonly JwtOptions _opciones = opciones.Value;

    /// <summary>
    /// Devuelve el token firmado y el instante en que deja de valer.
    /// </summary>
    public (string Token, DateTime ExpiraUtc) Generar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var expira = ahora.AddMinutes(_opciones.MinutosDeVida);

        // Los claims son los datos que el token lleva adentro. Van los mínimos para resolver
        // permisos sin volver a consultar la base en cada request: quién es, qué rol tiene y
        // sobre qué local puede operar.
        //
        // Ojo: el token es legible por cualquiera que lo tenga — está firmado, no cifrado.
        // Por eso acá no va nada secreto: ni contraseñas, ni el hash, ni datos sensibles.
        var claims = new List<Claim>
        {
            new(ClaimsPersonalizados.Sub, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimsPersonalizados.Email, usuario.Email),
            new(ClaimsPersonalizados.Nombre, usuario.Nombre),
            new(ClaimsPersonalizados.Rol, usuario.Rol.ToString())
        };

        // Sólo los vendedores tienen local. La ausencia del claim es significativa: es un admin.
        if (usuario.LocalId.HasValue)
        {
            claims.Add(new Claim(
                ClaimsPersonalizados.LocalId,
                usuario.LocalId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        // La firma es lo que hace confiable al token. Se calcula con una clave simétrica que
        // sólo conoce el servidor (viene de la configuración, nunca del código): si alguien
        // edita el contenido del token, la firma deja de coincidir y la API lo rechaza.
        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Issuer,
            audience: _opciones.Audience,
            claims: claims,
            notBefore: ahora,   // no vale antes de este instante
            expires: expira,    // ni después de este otro
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
