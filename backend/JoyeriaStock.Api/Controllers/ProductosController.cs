using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JoyeriaStock.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize]
public class ProductosController(ProductoService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductoDto>>> Listar(
        [FromQuery] bool incluirInactivos, CancellationToken ct)
        => Ok(await service.ListarAsync(incluirInactivos, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductoDto>> Obtener(int id, CancellationToken ct)
        => Ok(await service.ObtenerAsync(id, ct));

    /// <summary>
    /// Qué SKU le tocaría a un producto nuevo de esa categoría y línea, sin crearlo.
    /// Lo usa el formulario para mostrar el código antes de guardar.
    /// </summary>
    [HttpGet("proximo-sku")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<ActionResult<VistaPreviaSkuDto>> ProximoSku(
        [FromQuery] int categoriaId, [FromQuery] string? codigoLinea, CancellationToken ct)
        => Ok(await service.VistaPreviaSkuAsync(categoriaId, codigoLinea, ct));

    /// <summary>El SKU lo genera el sistema: no se manda en el cuerpo (regla 1).</summary>
    [HttpPost]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<ActionResult<ProductoDto>> Crear(GuardarProductoRequest request, CancellationToken ct)
    {
        var creado = await service.CrearAsync(request, ct);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, GuardarProductoRequest request, CancellationToken ct)
        => Ok(await service.ActualizarAsync(id, request, ct));

    /// <summary>Baja lógica: no borra la fila para no destruir el historial de movimientos.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        await service.DesactivarAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reactivar")]
    [Authorize(Roles = nameof(Domain.Enums.Rol.Admin))]
    public async Task<IActionResult> Reactivar(int id, CancellationToken ct)
    {
        await service.ReactivarAsync(id, ct);
        return NoContent();
    }
}
