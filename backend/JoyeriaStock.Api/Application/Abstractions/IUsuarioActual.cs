using JoyeriaStock.Api.Domain.Enums;

namespace JoyeriaStock.Api.Application.Abstractions;

/// <summary>
/// Quién está haciendo la operación. Los services dependen de esta interfaz y NO de HttpContext,
/// así la regla 5 (un vendedor sólo opera su local) se testea con un doble de prueba, sin levantar
/// un servidor HTTP ni fabricar tokens JWT.
/// </summary>
public interface IUsuarioActual
{
    int Id { get; }
    Rol Rol { get; }

    /// <summary>Local asignado. Nulo para Admin, que no está atado a ninguno.</summary>
    int? LocalId { get; }

    bool EsAdmin => Rol == Rol.Admin;
}
