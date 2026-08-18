using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Domain.Enums;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

/// <summary>
/// Único camino por el que puede cambiar el stock. Ningún otro service ni controller
/// escribe Stock.Cantidad: eso es lo que hace imposible eludir la regla 2.
/// </summary>
public class MovimientoService(AppDbContext db, IUsuarioActual usuario, TimeProvider reloj)
{
    public async Task<MovimientoDto> RegistrarAsync(CrearMovimientoRequest request, CancellationToken ct = default)
    {
        if (request.Cantidad <= 0)
            throw new ReglaNegocioException("La cantidad del movimiento debe ser mayor a cero.");

        if (!Enum.IsDefined(request.Tipo))
            throw new ReglaNegocioException("El tipo de movimiento no es válido.");

        // Los asientos de transferencia SIEMPRE nacen de a dos, desde TransferirAsync.
        // Permitir registrar uno suelto por acá dejaría mercadería saliendo de un local sin
        // entrar a ninguno, que es justamente el problema que la transferencia atómica resuelve.
        if (request.Tipo is TipoMovimiento.TransferenciaSalida or TipoMovimiento.TransferenciaEntrada)
        {
            throw new ReglaNegocioException(
                "Los movimientos de transferencia no se registran de a uno. Usá la operación de " +
                "transferencia, que descuenta del origen y suma en el destino en una sola transacción.");
        }

        // Regla 5: se verifica el permiso antes de tocar la base.
        AlcanceLocales.VerificarEscritura(usuario, request.LocalId);

        var producto = await db.Productos.AsNoTracking()
                           .FirstOrDefaultAsync(p => p.Id == request.ProductoId, ct)
                       ?? throw new RecursoNoEncontradoException($"No existe el producto {request.ProductoId}.");

        if (!producto.Activo)
            throw new ReglaNegocioException($"El producto '{producto.Nombre}' está dado de baja y no admite movimientos.");

        if (!await db.Locales.AnyAsync(l => l.Id == request.LocalId, ct))
            throw new RecursoNoEncontradoException($"No existe el local {request.LocalId}.");

        // El movimiento y la actualización del stock son un solo hecho: o pasan los dos o ninguno.
        // Esta transacción es también el andamiaje que va a necesitar la transferencia entre
        // locales (regla 6): serán dos movimientos acá adentro en lugar de uno.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var stock = await ObtenerOCrearStockAsync(request.ProductoId, request.LocalId, ct);

        var delta = request.Tipo == TipoMovimiento.Entrada ? request.Cantidad : -request.Cantidad;
        var cantidadResultante = stock.Cantidad + delta;

        // Regla 2: el stock nunca queda negativo.
        if (cantidadResultante < 0)
        {
            throw new ReglaNegocioException(
                $"Stock insuficiente de '{producto.Nombre}' (SKU {producto.Sku}): " +
                $"hay {stock.Cantidad} unidad(es) y se intentan retirar {request.Cantidad}.");
        }

        stock.Cantidad = cantidadResultante;

        var movimiento = new Movimiento
        {
            Tipo = request.Tipo,
            ProductoId = request.ProductoId,
            LocalId = request.LocalId,
            Cantidad = request.Cantidad,
            FechaUtc = reloj.GetUtcNow().UtcDateTime,
            UsuarioId = usuario.Id,
            Observacion = string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim()
        };

        // Sólo las ventas llevan plata, y el precio se congela en el momento de la venta:
        // si mañana cambia el precio del producto, esta venta sigue diciendo lo que se cobró.
        if (request.Tipo == TipoMovimiento.Venta)
        {
            movimiento.PrecioUnitarioAplicado = producto.PrecioBase;
            movimiento.Total = Math.Round(
                producto.PrecioBase * request.Cantidad, 2, MidpointRounding.AwayFromZero);
        }

        db.Movimientos.Add(movimiento);

        try
        {
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ErroresPostgres.EsCheckViolation(ex, ErroresPostgres.Restricciones.StockNoNegativo))
        {
            // La verificación de más arriba pasó, pero entre esa lectura y este UPDATE otra
            // operación descontó del mismo stock. El CHECK de la base frenó la transacción:
            // no se guardó nada y hay que reintentar con el número actualizado.
            throw new ConflictoConcurrenciaException(
                "Otra venta descontó este producto al mismo tiempo y el stock quedaría negativo. " +
                "Actualizá la pantalla y volvé a intentarlo.");
        }

        return await ObtenerAsync(movimiento.Id, cantidadResultante, ct);
    }

    /// <summary>
    /// Regla de negocio 6: traslado de mercadería entre locales, atómico.
    /// Descuenta del origen y suma en el destino en UNA sola transacción, y el origen
    /// nunca puede quedar negativo.
    /// </summary>
    /// <remarks>
    /// Sin esto, mover mercadería son dos operaciones sueltas: una Salida en el origen y
    /// una Entrada en el destino. Si la segunda falla, la mercadería se evapora — salió de
    /// un lado y no entró al otro, y el sistema queda mintiendo. Acá los dos asientos y las
    /// dos actualizaciones de stock viven en la misma transacción: o pasan las cuatro cosas
    /// o no pasa ninguna.
    /// </remarks>
    public async Task<TransferenciaDto> TransferirAsync(
        CrearTransferenciaRequest request,
        CancellationToken ct = default)
    {
        if (request.Cantidad <= 0)
            throw new ReglaNegocioException("La cantidad a transferir debe ser mayor a cero.");

        if (request.LocalOrigenId == request.LocalDestinoId)
            throw new ReglaNegocioException("El local de origen y el de destino tienen que ser distintos.");

        // Una transferencia toca el stock de DOS locales. Un vendedor sólo puede operar el
        // suyo (regla 5), así que por definición no puede autorizar la operación completa:
        // estaría modificando un local que no le corresponde.
        if (usuario.Rol != Rol.Admin)
        {
            throw new AccesoDenegadoException(
                "Sólo un administrador puede transferir entre locales, porque la operación " +
                "afecta el stock de dos locales a la vez.");
        }

        var producto = await db.Productos.AsNoTracking()
                           .FirstOrDefaultAsync(p => p.Id == request.ProductoId, ct)
                       ?? throw new RecursoNoEncontradoException($"No existe el producto {request.ProductoId}.");

        if (!producto.Activo)
            throw new ReglaNegocioException($"El producto '{producto.Nombre}' está dado de baja y no se puede transferir.");

        var origen = await db.Locales.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocalOrigenId, ct)
                     ?? throw new RecursoNoEncontradoException($"No existe el local de origen {request.LocalOrigenId}.");

        var destino = await db.Locales.AsNoTracking().FirstOrDefaultAsync(l => l.Id == request.LocalDestinoId, ct)
                      ?? throw new RecursoNoEncontradoException($"No existe el local de destino {request.LocalDestinoId}.");

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);

        var stockOrigen = await ObtenerOCrearStockAsync(request.ProductoId, request.LocalOrigenId, ct);

        if (stockOrigen.Cantidad < request.Cantidad)
        {
            throw new ReglaNegocioException(
                $"Stock insuficiente de '{producto.Nombre}' (SKU {producto.Sku}) en {origen.Nombre}: " +
                $"hay {stockOrigen.Cantidad} unidad(es) y se intentan transferir {request.Cantidad}.");
        }

        var stockDestino = await ObtenerOCrearStockAsync(request.ProductoId, request.LocalDestinoId, ct);

        stockOrigen.Cantidad -= request.Cantidad;
        stockDestino.Cantidad += request.Cantidad;

        // Los dos asientos comparten el mismo identificador: es lo que permite leer el
        // traslado como una sola operación y no como dos movimientos sin relación.
        var transferenciaId = Guid.NewGuid();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var observacion = string.IsNullOrWhiteSpace(request.Observacion) ? null : request.Observacion.Trim();

        db.Movimientos.AddRange(
            new Movimiento
            {
                Tipo = TipoMovimiento.TransferenciaSalida,
                ProductoId = request.ProductoId,
                LocalId = request.LocalOrigenId,
                Cantidad = request.Cantidad,
                FechaUtc = ahora,
                UsuarioId = usuario.Id,
                TransferenciaId = transferenciaId,
                Observacion = observacion ?? $"Traslado a {destino.Nombre}"
            },
            new Movimiento
            {
                Tipo = TipoMovimiento.TransferenciaEntrada,
                ProductoId = request.ProductoId,
                LocalId = request.LocalDestinoId,
                Cantidad = request.Cantidad,
                FechaUtc = ahora,
                UsuarioId = usuario.Id,
                TransferenciaId = transferenciaId,
                Observacion = observacion ?? $"Traslado desde {origen.Nombre}"
            });

        try
        {
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ErroresPostgres.EsCheckViolation(ex, ErroresPostgres.Restricciones.StockNoNegativo))
        {
            throw new ConflictoConcurrenciaException(
                "Otra operación descontó este producto del local de origen al mismo tiempo. " +
                "Actualizá la pantalla y volvé a intentarlo.");
        }

        return new TransferenciaDto(
            transferenciaId,
            producto.Id,
            producto.Sku,
            producto.Nombre,
            request.Cantidad,
            ahora,
            origen.Id,
            origen.Nombre,
            stockOrigen.Cantidad,
            destino.Id,
            destino.Nombre,
            stockDestino.Cantidad);
    }

    /// <summary>
    /// Trae la fila de stock del par producto+local, o la crea en cero si es la primera vez
    /// que ese producto toca ese local.
    /// </summary>
    private async Task<Stock> ObtenerOCrearStockAsync(int productoId, int localId, CancellationToken ct)
    {
        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.LocalId == localId, ct);

        if (stock is null)
        {
            stock = new Stock { ProductoId = productoId, LocalId = localId, Cantidad = 0 };
            db.Stocks.Add(stock);
        }

        return stock;
    }

    public async Task<List<MovimientoDto>> ListarAsync(
        int? localId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken ct = default)
    {
        var localEfectivo = AlcanceLocales.ResolverParaLectura(usuario, localId);

        var query = db.Movimientos.AsNoTracking().AsQueryable();

        if (localEfectivo.HasValue)
            query = query.Where(m => m.LocalId == localEfectivo.Value);

        if (productoId.HasValue)
            query = query.Where(m => m.ProductoId == productoId.Value);

        return await query
            .OrderByDescending(m => m.FechaUtc)
            .ThenByDescending(m => m.Id)
            .Take(Math.Clamp(limite, 1, 500))
            .Select(m => new MovimientoDto(
                m.Id, m.Tipo, m.ProductoId, m.Producto.Sku, m.Producto.Nombre,
                m.LocalId, m.Local.Nombre, m.Cantidad, m.FechaUtc, m.Usuario.Nombre,
                m.Observacion, m.PrecioUnitarioAplicado, m.Total, 0, m.TransferenciaId))
            .ToListAsync(ct);
    }

    private async Task<MovimientoDto> ObtenerAsync(int id, int cantidadResultante, CancellationToken ct)
        => await db.Movimientos.AsNoTracking()
               .Where(m => m.Id == id)
               .Select(m => new MovimientoDto(
                   m.Id, m.Tipo, m.ProductoId, m.Producto.Sku, m.Producto.Nombre,
                   m.LocalId, m.Local.Nombre, m.Cantidad, m.FechaUtc, m.Usuario.Nombre,
                   m.Observacion, m.PrecioUnitarioAplicado, m.Total, cantidadResultante,
                   m.TransferenciaId))
               .FirstAsync(ct);
}
