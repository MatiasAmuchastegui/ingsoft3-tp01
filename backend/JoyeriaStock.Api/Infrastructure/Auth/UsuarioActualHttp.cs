using System.Globalization;
using System.Security.Claims;
using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Domain.Enums;

namespace JoyeriaStock.Api.Infrastructure.Auth;

/// <summary>
/// Implementación de <see cref="IUsuarioActual"/> que lee los claims del JWT.
/// Es la ÚNICA clase que conoce HttpContext; los services dependen sólo de la interfaz,
/// y por eso se pueden testear sin levantar un servidor.
/// </summary>
public class UsuarioActualHttp(IHttpContextAccessor accessor) : IUsuarioActual
{
    public int Id => LeerEntero(ClaimsPersonalizados.Sub)
        ?? throw new InvalidOperationException("No hay un usuario autenticado en el contexto actual.");

    public Rol Rol
    {
        get
        {
            var valor = Principal?.FindFirstValue(ClaimsPersonalizados.Rol);
            return Enum.TryParse<Rol>(valor, ignoreCase: true, out var rol)
                ? rol
                : throw new InvalidOperationException("El token no trae un rol válido.");
        }
    }

    public int? LocalId => LeerEntero(ClaimsPersonalizados.LocalId);

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    private int? LeerEntero(string claim)
    {
        var valor = Principal?.FindFirstValue(claim);
        return int.TryParse(valor, CultureInfo.InvariantCulture, out var numero) ? numero : null;
    }
}
