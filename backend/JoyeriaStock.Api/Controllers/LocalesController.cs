using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

[ApiController]
[Route("api/locales")]
[Authorize]
public class LocalesController(LocalService service) : ControllerBase
{
    /// <summary>Locales que el usuario puede ver: los 3 si es Admin, sólo el propio si es Vendedor.</summary>
    [HttpGet]
    public async Task<ActionResult<List<LocalDto>>> Listar(CancellationToken ct)
        => Ok(await service.ListarVisiblesAsync(ct));
}
