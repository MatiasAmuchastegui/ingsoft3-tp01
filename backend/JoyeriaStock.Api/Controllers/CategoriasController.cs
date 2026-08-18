using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

/// <summary>
/// El catálogo es global, así que sólo lo administra un Admin. Un vendedor puede leerlo
/// (necesita ver las categorías) pero no modificarlo.
/// </summary>
[ApiController]
[Route("api/categorias")]
[Authorize]
public class CategoriasController(CategoriaService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoriaDto>>> Listar(CancellationToken ct)
        => Ok(await service.ListarAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoriaDto>> Obtener(int id, CancellationToken ct)
        => Ok(await service.ObtenerAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<ActionResult<CategoriaDto>> Crear(GuardarCategoriaRequest request, CancellationToken ct)
    {
        var creada = await service.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<ActionResult<CategoriaDto>> Actualizar(int id, GuardarCategoriaRequest request, CancellationToken ct)
        => Ok(await service.ActualizarAsync(id, request, ct));

    /// <summary>Falla con 409 si la categoría tiene productos asociados (regla 3).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await service.EliminarAsync(id, ct);
        return NoContent();
    }
}
