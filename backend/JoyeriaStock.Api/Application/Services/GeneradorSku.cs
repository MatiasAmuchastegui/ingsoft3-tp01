using System.Globalization;
using System.Text.RegularExpressions;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

/// <summary>
/// Arma los códigos de producto: PREFIJO-NNNN, con el número puesto por el sistema.
/// </summary>
/// <remarks>
/// El prefijo se compone del código de la categoría (Relojes → REL) más un código opcional
/// de línea o marca del producto (Citizen → CT), dando RELCT-0001, RELCT-0002…
///
/// El número es correlativo **por prefijo**: cada serie lleva su propia numeración, así que
/// agregar una marca nueva no continúa la numeración de otra.
/// </remarks>
public partial class GeneradorSku(AppDbContext db)
{
    /// <summary>Cantidad de dígitos del correlativo. 4 alcanza para 9999 piezas por serie.</summary>
    private const int Digitos = 4;

    /// <summary>Sólo letras y números: el SKU va impreso en etiquetas y se dicta por teléfono.</summary>
    [GeneratedRegex("^[A-Z0-9]+$")]
    private static partial Regex FormatoCodigo();

    /// <summary>
    /// Normaliza un código escrito por una persona: saca espacios y guiones, y lo pasa a
    /// mayúsculas, para que "ct", "CT" y " Ct " sean el mismo código.
    /// </summary>
    public static string NormalizarCodigo(string? codigo, string nombreCampo, int maximo)
    {
        var limpio = (codigo ?? string.Empty)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();

        if (limpio.Length == 0)
            throw new ReglaNegocioException($"{nombreCampo} es obligatorio.");

        if (limpio.Length > maximo)
            throw new ReglaNegocioException($"{nombreCampo} no puede superar los {maximo} caracteres.");

        if (!FormatoCodigo().IsMatch(limpio))
            throw new ReglaNegocioException($"{nombreCampo} sólo puede tener letras y números.");

        return limpio;
    }

    /// <summary>Igual que el anterior, pero acepta vacío y devuelve null.</summary>
    public static string? NormalizarCodigoOpcional(string? codigo, string nombreCampo, int maximo)
        => string.IsNullOrWhiteSpace(codigo)
            ? null
            : NormalizarCodigo(codigo, nombreCampo, maximo);

    /// <summary>
    /// Devuelve el próximo SKU libre para la categoría y el código de línea indicados.
    /// </summary>
    public async Task<string> GenerarAsync(int categoriaId, string? codigoLinea, CancellationToken ct = default)
    {
        var prefijoCategoria = await db.Categorias
                                   .Where(c => c.Id == categoriaId)
                                   .Select(c => c.PrefijoSku)
                                   .FirstOrDefaultAsync(ct)
                               ?? throw new ReglaNegocioException($"No existe la categoría {categoriaId}.");

        var prefijo = prefijoCategoria + (codigoLinea ?? string.Empty);
        var comienzo = prefijo + "-";

        // Se buscan los SKU ya usados con este prefijo y se toma el número más alto.
        // No se cuentan las filas: si alguna vez se borra un producto del medio, contar
        // devolvería un número ya usado y chocaría contra el índice único.
        var existentes = await db.Productos
            .Where(p => p.Sku.StartsWith(comienzo))
            .Select(p => p.Sku)
            .ToListAsync(ct);

        var ultimo = 0;
        foreach (var sku in existentes)
        {
            var parteNumerica = sku[comienzo.Length..];
            // Tolera series viejas con otra cantidad de dígitos (REL-001 y REL-0001 conviven).
            if (int.TryParse(parteNumerica, NumberStyles.None, CultureInfo.InvariantCulture, out var numero)
                && numero > ultimo)
            {
                ultimo = numero;
            }
        }

        var siguiente = ultimo + 1;
        if (siguiente >= (int)Math.Pow(10, Digitos))
        {
            throw new ReglaNegocioException(
                $"La serie {prefijo} llegó a su último número. Usá un código de línea distinto.");
        }

        return comienzo + siguiente.ToString($"D{Digitos}", CultureInfo.InvariantCulture);
    }
}
