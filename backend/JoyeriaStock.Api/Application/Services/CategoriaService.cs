using System.Linq.Expressions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

public class CategoriaService(AppDbContext db)
{
    private static readonly Expression<Func<Categoria, CategoriaDto>> Proyeccion =
        c => new CategoriaDto(c.Id, c.Nombre, c.PrefijoSku, c.Productos.Count);

    public async Task<List<CategoriaDto>> ListarAsync(CancellationToken ct = default)
        => await db.Categorias
            .OrderBy(c => c.Nombre)
            .Select(Proyeccion)
            .ToListAsync(ct);

    public async Task<CategoriaDto> ObtenerAsync(int id, CancellationToken ct = default)
        => await db.Categorias
               .Where(c => c.Id == id)
               .Select(Proyeccion)
               .FirstOrDefaultAsync(ct)
           ?? throw new RecursoNoEncontradoException($"No existe la categoría {id}.");

    public async Task<CategoriaDto> CrearAsync(GuardarCategoriaRequest request, CancellationToken ct = default)
    {
        var nombre = NormalizarNombre(request.Nombre);
        var prefijo = GeneradorSku.NormalizarCodigo(request.PrefijoSku, "El prefijo de SKU", 6);

        await VerificarNombreDisponibleAsync(nombre, idExcluido: null, ct);
        await VerificarPrefijoDisponibleAsync(prefijo, idExcluido: null, ct);

        var categoria = new Categoria { Nombre = nombre, PrefijoSku = prefijo };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct);

        return new CategoriaDto(categoria.Id, categoria.Nombre, categoria.PrefijoSku, 0);
    }

    public async Task<CategoriaDto> ActualizarAsync(int id, GuardarCategoriaRequest request, CancellationToken ct = default)
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new RecursoNoEncontradoException($"No existe la categoría {id}.");

        var nombre = NormalizarNombre(request.Nombre);
        var prefijo = GeneradorSku.NormalizarCodigo(request.PrefijoSku, "El prefijo de SKU", 6);

        await VerificarNombreDisponibleAsync(nombre, idExcluido: id, ct);
        await VerificarPrefijoDisponibleAsync(prefijo, idExcluido: id, ct);

        // Cambiar el prefijo con productos ya cargados dejaría códigos viejos que no se
        // corresponden con su categoría, y los SKU no se pueden reescribir porque están
        // impresos en las etiquetas de las piezas.
        if (prefijo != categoria.PrefijoSku)
        {
            var cantidadProductos = await db.Productos.CountAsync(p => p.CategoriaId == id, ct);
            if (cantidadProductos > 0)
            {
                throw new ReglaNegocioException(
                    $"No se puede cambiar el prefijo de '{categoria.Nombre}' porque ya tiene " +
                    $"{cantidadProductos} producto(s) con códigos {categoria.PrefijoSku}-… emitidos.");
            }
        }

        categoria.Nombre = nombre;
        categoria.PrefijoSku = prefijo;
        await db.SaveChangesAsync(ct);

        return await ObtenerAsync(id, ct);
    }

    /// <summary>
    /// Regla de negocio 3: no se puede eliminar una categoría que tiene productos asociados.
    /// </summary>
    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new RecursoNoEncontradoException($"No existe la categoría {id}.");

        // Cuenta TODOS los productos, incluidos los dados de baja lógica: un producto inactivo
        // sigue teniendo historial y sigue apuntando a esta categoría.
        var cantidadProductos = await db.Productos.CountAsync(p => p.CategoriaId == id, ct);
        if (cantidadProductos > 0)
        {
            throw new ReglaNegocioException(
                $"No se puede eliminar la categoría '{categoria.Nombre}' porque tiene " +
                $"{cantidadProductos} producto(s) asociado(s). Reasignalos o eliminalos primero.");
        }

        db.Categorias.Remove(categoria);
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizarNombre(string nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
            throw new ReglaNegocioException("El nombre de la categoría es obligatorio.");
        if (limpio.Length > 80)
            throw new ReglaNegocioException("El nombre de la categoría no puede superar los 80 caracteres.");
        return limpio;
    }

    private async Task VerificarNombreDisponibleAsync(string nombre, int? idExcluido, CancellationToken ct)
    {
        var nombreLower = nombre.ToLowerInvariant();
        var query = db.Categorias.Where(c => c.Nombre.ToLower() == nombreLower);

        // Explícito a propósito: `c.Id != idExcluido` con un int? nulo depende de cómo EF
        // resuelva la semántica de nulos, y no vale la pena apostar a eso.
        if (idExcluido.HasValue)
            query = query.Where(c => c.Id != idExcluido.Value);

        if (await query.AnyAsync(ct))
            throw new ReglaNegocioException($"Ya existe una categoría con el nombre '{nombre}'.");
    }

    private async Task VerificarPrefijoDisponibleAsync(string prefijo, int? idExcluido, CancellationToken ct)
    {
        var query = db.Categorias.Where(c => c.PrefijoSku == prefijo);

        if (idExcluido.HasValue)
            query = query.Where(c => c.Id != idExcluido.Value);

        if (await query.AnyAsync(ct))
        {
            throw new ReglaNegocioException(
                $"El prefijo '{prefijo}' ya lo usa otra categoría. Cada categoría necesita el suyo " +
                "para que el código diga a qué categoría pertenece la pieza.");
        }
    }
}
