using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
[Authorize]
public class MovimientosController(MovimientoService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MovimientoDto>>> Listar(
        [FromQuery] int? localId,
        [FromQuery] int? productoId,
        [FromQuery] int limite,
        CancellationToken ct)
        => Ok(await service.ListarAsync(localId, productoId, limite == 0 ? 100 : limite, ct));

    /// <summary>
    /// Registra una entrada, salida o venta. Devuelve 409 si el stock quedaría negativo (regla 2)
    /// y 403 si un vendedor intenta operar un local ajeno (regla 5).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MovimientoDto>> Registrar(CrearMovimientoRequest request, CancellationToken ct)
        => Ok(await service.RegistrarAsync(request, ct));

    /// <summary>
    /// Traslada mercadería de un local a otro (regla 6). Descuenta del origen y suma en el
    /// destino en una sola transacción: o se hacen las dos cosas o no se hace ninguna.
    /// Devuelve 409 si el origen no tiene stock suficiente y 403 si no sos administrador.
    /// </summary>
    [HttpPost("transferencia")]
    public async Task<ActionResult<TransferenciaDto>> Transferir(
        CrearTransferenciaRequest request, CancellationToken ct)
        => Ok(await service.TransferirAsync(request, ct));
}
