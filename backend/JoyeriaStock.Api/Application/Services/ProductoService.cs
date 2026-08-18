using System.Linq.Expressions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

public class ProductoService(AppDbContext db, GeneradorSku generadorSku)
{
    /// <summary>
    /// Proyección compartida. Va como Expression y no como método: EF necesita el árbol de
    /// expresión para traducirlo a SQL — una llamada a método no la puede traducir.
    /// </summary>
    private static readonly Expression<Func<Producto, ProductoDto>> Proyeccion = p => new ProductoDto(
        p.Id, p.Sku, p.CodigoLinea, p.Nombre, p.CategoriaId, p.Categoria.Nombre, p.PrecioBase,
        p.UmbralStockBajo, p.Activo);

    public async Task<List<ProductoDto>> ListarAsync(bool incluirInactivos = false, CancellationToken ct = default)
    {
        var query = db.Productos.AsNoTracking().AsQueryable();
        if (!incluirInactivos)
            query = query.Where(p => p.Activo);

        return await query
            .OrderBy(p => p.Nombre)
            .Select(Proyeccion)
            .ToListAsync(ct);
    }

    public async Task<ProductoDto> ObtenerAsync(int id, CancellationToken ct = default)
        => await db.Productos.AsNoTracking()
               .Where(p => p.Id == id)
               .Select(Proyeccion)
               .FirstOrDefaultAsync(ct)
           ?? throw new RecursoNoEncontradoException($"No existe el producto {id}.");

    /// <summary>
    /// Da de alta el producto y le asigna el SKU. El código lo pone el sistema:
    /// prefijo de la categoría + código de línea opcional + correlativo.
    /// </summary>
    public async Task<ProductoDto> CrearAsync(GuardarProductoRequest request, CancellationToken ct = default)
    {
        Validar(request);
        await VerificarCategoriaExisteAsync(request.CategoriaId, ct);

        var codigoLinea = GeneradorSku.NormalizarCodigoOpcional(
            request.CodigoLinea, "El código de línea", 6);

        // Entre calcular el próximo número y guardarlo hay una ventana en la que otro alta
        // puede quedarse con ese mismo número. El índice único de la base lo detecta; acá
        // se reintenta con el siguiente libre en lugar de hacerle repetir la carga al usuario.
        const int intentos = 5;
        for (var intento = 1; ; intento++)
        {
            var sku = await generadorSku.GenerarAsync(request.CategoriaId, codigoLinea, ct);

            var producto = new Producto
            {
                Sku = sku,
                CodigoLinea = codigoLinea,
                Nombre = request.Nombre.Trim(),
                CategoriaId = request.CategoriaId,
                PrecioBase = request.PrecioBase,
                UmbralStockBajo = request.UmbralStockBajo,
                Activo = true
            };

            db.Productos.Add(producto);

            try
            {
                await db.SaveChangesAsync(ct);
                return await ObtenerAsync(producto.Id, ct);
            }
            catch (DbUpdateException ex) when (
                intento < intentos &&
                ErroresPostgres.EsUniqueViolation(ex, ErroresPostgres.Restricciones.ProductoSkuUnico))
            {
                // Otro alta se quedó con este número. Se descarta la entidad fallida del
                // contexto y se vuelve a pedir el siguiente.
                db.Entry(producto).State = EntityState.Detached;
            }
        }
    }

    /// <summary>Muestra qué SKU le tocaría a un producto nuevo, sin crearlo.</summary>
    public async Task<VistaPreviaSkuDto> VistaPreviaSkuAsync(
        int categoriaId, string? codigoLinea, CancellationToken ct = default)
    {
        var codigo = GeneradorSku.NormalizarCodigoOpcional(codigoLinea, "El código de línea", 6);
        return new VistaPreviaSkuDto(await generadorSku.GenerarAsync(categoriaId, codigo, ct));
    }

    public async Task<ProductoDto> ActualizarAsync(int id, GuardarProductoRequest request, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new RecursoNoEncontradoException($"No existe el producto {id}.");

        Validar(request);

        // El SKU NO se toca al editar: está impreso en la etiqueta de la pieza y aparece en
        // todo su historial de movimientos. Reescribirlo rompería la trazabilidad.
        //
        // Y como el SKU deriva del prefijo de la categoría, cambiar de categoría dejaría un
        // código que miente sobre a qué categoría pertenece. Si de verdad hay que
        // recategorizar algo, se da de baja y se crea de nuevo, que además deja rastro.
        if (request.CategoriaId != producto.CategoriaId)
        {
            throw new ReglaNegocioException(
                $"No se puede cambiar la categoría de un producto ya creado: su código " +
                $"({producto.Sku}) deriva del prefijo de la categoría original. " +
                "Dalo de baja y creá uno nuevo en la categoría que corresponda.");
        }

        producto.Nombre = request.Nombre.Trim();
        producto.PrecioBase = request.PrecioBase;
        producto.UmbralStockBajo = request.UmbralStockBajo;

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, ct);
    }

    /// <summary>
    /// Baja lógica. No se borra la fila porque los movimientos históricos la referencian:
    /// borrarla destruiría la auditoría de ventas.
    /// </summary>
    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new RecursoNoEncontradoException($"No existe el producto {id}.");

        if (!producto.Activo)
            return;

        producto.Activo = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task ReactivarAsync(int id, CancellationToken ct = default)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new RecursoNoEncontradoException($"No existe el producto {id}.");

        producto.Activo = true;
        await db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private static void Validar(GuardarProductoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            throw new ReglaNegocioException("El nombre del producto es obligatorio.");

        if (request.PrecioBase <= 0)
            throw new ReglaNegocioException("El precio base debe ser mayor a cero.");

        if (request.UmbralStockBajo < 0)
            throw new ReglaNegocioException("El umbral de stock bajo no puede ser negativo.");
    }

    private async Task VerificarCategoriaExisteAsync(int categoriaId, CancellationToken ct)
    {
        if (!await db.Categorias.AnyAsync(c => c.Id == categoriaId, ct))
            throw new ReglaNegocioException($"No existe la categoría {categoriaId}.");
    }
}
