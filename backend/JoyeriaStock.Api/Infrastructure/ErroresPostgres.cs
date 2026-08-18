using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JoyeriaStock.Api.Infrastructure;

/// <summary>
/// Reconoce violaciones de restricciones de PostgreSQL para poder traducirlas a mensajes
/// del dominio.
/// </summary>
/// <remarks>
/// Los services verifican las reglas antes de escribir, pero entre esa verificación y el
/// INSERT hay una ventana en la que otra operación puede meterse. Las restricciones de la
/// base cierran esa ventana; estos helpers convierten el error crudo en algo que el usuario
/// pueda entender.
/// </remarks>
public static class ErroresPostgres
{
    // Códigos SQLSTATE estándar.
    private const string ViolacionUnique = "23505";
    private const string ViolacionCheck = "23514";

    public static bool EsUniqueViolation(DbUpdateException ex, string nombreIndice)
        => ex.InnerException is PostgresException { SqlState: ViolacionUnique } pg
           && string.Equals(pg.ConstraintName, nombreIndice, StringComparison.OrdinalIgnoreCase);

    public static bool EsCheckViolation(DbUpdateException ex, string nombreRestriccion)
        => ex.InnerException is PostgresException { SqlState: ViolacionCheck } pg
           && string.Equals(pg.ConstraintName, nombreRestriccion, StringComparison.OrdinalIgnoreCase);

    /// <summary>Nombres de las restricciones, tal como los genera la migración.</summary>
    public static class Restricciones
    {
        public const string StockNoNegativo = "ck_stocks_cantidad_no_negativa";
        public const string ProductoSkuUnico = "ix_productos_sku";
    }
}
