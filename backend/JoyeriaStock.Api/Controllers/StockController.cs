using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(StockService service) : ControllerBase
{
    /// <summary>
    /// Stock por local. Un Admin sin <paramref name="localId"/> recibe los 3 locales;
    /// un Vendedor recibe siempre el suyo, y pedir otro devuelve 403 (regla 5).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<StockDto>>> Listar(
        [FromQuery] int? localId,
        [FromQuery] string? busqueda,
        [FromQuery] bool soloStockBajo,
        CancellationToken ct)
        => Ok(await service.ListarAsync(localId, busqueda, soloStockBajo, ct));
}
