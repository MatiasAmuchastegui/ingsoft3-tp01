using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

public class StockService(AppDbContext db, IUsuarioActual usuario)
{
    /// <summary>
    /// Listado de stock: TODOS los productos activos en TODOS los locales visibles, con la
    /// cantidad que haya y 0 donde el producto nunca se movió en ese local.
    /// El alcance por rol (regla 5) se aplica ANTES de consultar, no filtrando después.
    /// </summary>
    /// <remarks>
    /// La consulta parte de productos × locales y hace LEFT JOIN contra stocks, en lugar de
    /// partir de las filas de stocks existentes. La diferencia no es cosmética:
    ///
    /// una fila de stocks sólo nace cuando MovimientoService registra el primer movimiento
    /// de ese producto en ese local. Partiendo de stocks, un producto recién creado no
    /// aparecía en la pantalla, y como los movimientos se registran desde una fila de esa
    /// misma pantalla, no había forma de darle entrada: quedaba invisible para siempre.
    ///
    /// Así también se arregla solo el caso simétrico — abrir un local nuevo — sin tener que
    /// crear filas en stocks para todo el catálogo.
    /// </remarks>
    public async Task<List<StockDto>> ListarAsync(
        int? localId = null,
        string? busqueda = null,
        bool soloStockBajo = false,
        CancellationToken ct = default)
    {
        var localEfectivo = AlcanceLocales.ResolverParaLectura(usuario, localId);

        var locales = db.Locales.AsNoTracking().AsQueryable();
        if (localEfectivo.HasValue)
            locales = locales.Where(l => l.Id == localEfectivo.Value);

        var productos = db.Productos.AsNoTracking().Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var termino = busqueda.Trim().ToLowerInvariant();
            productos = productos.Where(p =>
                p.Nombre.ToLower().Contains(termino) ||
                p.Sku.ToLower().Contains(termino));
        }

        var pares =
            from p in productos
            from l in locales
            join s in db.Stocks.AsNoTracking()
                on new { ProductoId = p.Id, LocalId = l.Id }
                equals new { s.ProductoId, s.LocalId }
                into coincidencias
            from s in coincidencias.DefaultIfEmpty()
            select new
            {
                Producto = p,
                Local = l,
                // Sin fila de stock, la existencia es cero: es lo mismo que decir que el
                // producto existe en el catálogo pero todavía no llegó a ese local.
                Cantidad = s != null ? s.Cantidad : 0
            };

        if (soloStockBajo)
            pares = pares.Where(x => x.Cantidad <= x.Producto.UmbralStockBajo);

        return await pares
            .OrderBy(x => x.Local.Nombre)
            .ThenBy(x => x.Producto.Nombre)
            .Select(x => new StockDto(
                x.Producto.Id,
                x.Producto.Sku,
                x.Producto.Nombre,
                x.Producto.Categoria.Nombre,
                x.Local.Id,
                x.Local.Nombre,
                x.Cantidad,
                x.Producto.UmbralStockBajo,
                x.Cantidad <= x.Producto.UmbralStockBajo,
                x.Producto.PrecioBase))
            .ToListAsync(ct);
    }

    /// <summary>Cantidad de un producto en un local puntual. 0 si nunca tuvo stock ahí.</summary>
    public async Task<int> ObtenerCantidadAsync(int productoId, int localId, CancellationToken ct = default)
    {
        AlcanceLocales.ResolverParaLectura(usuario, localId);

        return await db.Stocks.AsNoTracking()
            .Where(s => s.ProductoId == productoId && s.LocalId == localId)
            .Select(s => s.Cantidad)
            .FirstOrDefaultAsync(ct);
    }
}
