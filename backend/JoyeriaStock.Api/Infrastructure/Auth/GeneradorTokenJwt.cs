using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JoyeriaStock.Api.Infrastructure.Auth;

public class GeneradorTokenJwt(IOptions<JwtOptions> opciones, TimeProvider reloj) : IGeneradorToken
{
    private readonly JwtOptions _opciones = opciones.Value;

    public (string Token, DateTime ExpiraUtc) Generar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var expira = ahora.AddMinutes(_opciones.MinutosDeVida);

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

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opciones.Issuer,
            audience: _opciones.Audience,
            claims: claims,
            notBefore: ahora,
            expires: expira,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
