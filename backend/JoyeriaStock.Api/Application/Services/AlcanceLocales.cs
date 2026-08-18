using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Domain;

namespace JoyeriaStock.Api.Application.Services;

/// <summary>
/// Regla de negocio 5, en un solo lugar: un Vendedor sólo ve y opera su propio local;
/// un Admin ve y opera todos.
/// </summary>
/// <remarks>
/// Está acá y no repartida por los services para que exista UN único punto donde se decide
/// el alcance. Si mañana aparece un rol "Encargado de zona" con varios locales, se cambia acá.
/// </remarks>
public static class AlcanceLocales
{
    /// <summary>
    /// Resuelve qué local se debe consultar. Devuelve null cuando hay que traer todos los locales
    /// (sólo posible para Admin sin filtro explícito).
    /// </summary>
    /// <param name="localIdPedido">Local que pidió el cliente, o null si no filtró.</param>
    public static int? ResolverParaLectura(IUsuarioActual usuario, int? localIdPedido)
    {
        if (usuario.Rol == Domain.Enums.Rol.Admin)
            return localIdPedido; // null = todos los locales

        var localPropio = LocalPropioObligatorio(usuario);

        // Un vendedor que no filtra ve su local. Si filtra por otro, se le niega
        // explícitamente en lugar de devolverle su local en silencio: fallar callado
        // esconde bugs del frontend.
        if (localIdPedido.HasValue && localIdPedido.Value != localPropio)
            throw new AccesoDenegadoException("Sólo podés consultar el stock de tu propio local.");

        return localPropio;
    }

    /// <summary>Verifica que el usuario pueda escribir (movimientos) sobre el local indicado.</summary>
    public static void VerificarEscritura(IUsuarioActual usuario, int localId)
    {
        if (usuario.Rol == Domain.Enums.Rol.Admin)
            return;

        if (LocalPropioObligatorio(usuario) != localId)
            throw new AccesoDenegadoException("Sólo podés registrar movimientos en tu propio local.");
    }

    private static int LocalPropioObligatorio(IUsuarioActual usuario)
        => usuario.LocalId
           ?? throw new AccesoDenegadoException(
               "Tu usuario no tiene un local asignado. Pedile a un administrador que te asigne uno.");
}
