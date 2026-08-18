using JoyeriaStock.Api.Domain.Enums;

namespace JoyeriaStock.Api.Application.Dtos;

// ---------- Auth ----------

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, DateTime ExpiraUtc, UsuarioDto Usuario);

public record UsuarioDto(int Id, string Email, string Nombre, Rol Rol, int? LocalId, string? LocalNombre);

// ---------- Locales ----------

public record LocalDto(int Id, string Nombre, string Direccion);

// ---------- Categorías ----------

public record CategoriaDto(int Id, string Nombre, string PrefijoSku, int CantidadProductos);

public record GuardarCategoriaRequest(string Nombre, string PrefijoSku);

// ---------- Productos ----------

public record ProductoDto(
    int Id,
    string Sku,
    string? CodigoLinea,
    string Nombre,
    int CategoriaId,
    string CategoriaNombre,
    decimal PrecioBase,
    int UmbralStockBajo,
    bool Activo);

/// <summary>
/// El SKU no viaja acá: lo genera el sistema al crear, y al actualizar no se toca.
/// Lo único que el usuario aporta al código es el <see cref="CodigoLinea"/> opcional.
/// </summary>
public record GuardarProductoRequest(
    string Nombre,
    int CategoriaId,
    string? CodigoLinea,
    decimal PrecioBase,
    int UmbralStockBajo);

/// <summary>Vista previa del SKU que se generaría, para mostrarla antes de guardar.</summary>
public record VistaPreviaSkuDto(string Sku);

// ---------- Stock ----------

public record StockDto(
    int ProductoId,
    string Sku,
    string ProductoNombre,
    string CategoriaNombre,
    int LocalId,
    string LocalNombre,
    int Cantidad,
    int UmbralStockBajo,
    bool StockBajo,
    decimal PrecioBase);

// ---------- Movimientos ----------

public record CrearMovimientoRequest(
    TipoMovimiento Tipo,
    int ProductoId,
    int LocalId,
    int Cantidad,
    string? Observacion);

public record MovimientoDto(
    int Id,
    TipoMovimiento Tipo,
    int ProductoId,
    string Sku,
    string ProductoNombre,
    int LocalId,
    string LocalNombre,
    int Cantidad,
    DateTime FechaUtc,
    string UsuarioNombre,
    string? Observacion,
    decimal? PrecioUnitarioAplicado,
    decimal? Total,
    int CantidadResultante,
    Guid? TransferenciaId);

// ---------- Transferencias entre locales ----------

public record CrearTransferenciaRequest(
    int ProductoId,
    int LocalOrigenId,
    int LocalDestinoId,
    int Cantidad,
    string? Observacion);

public record TransferenciaDto(
    Guid TransferenciaId,
    int ProductoId,
    string Sku,
    string ProductoNombre,
    int Cantidad,
    DateTime FechaUtc,
    int LocalOrigenId,
    string LocalOrigenNombre,
    int CantidadResultanteOrigen,
    int LocalDestinoId,
    string LocalDestinoNombre,
    int CantidadResultanteDestino);

