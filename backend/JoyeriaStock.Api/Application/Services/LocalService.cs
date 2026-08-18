using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Dtos;
using JoyeriaStock.Api.Domain.Enums;
using JoyeriaStock.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Application.Services;

public class LocalService(AppDbContext db, IUsuarioActual usuario)
{
    /// <summary>
    /// Locales que el usuario puede ver. Un Admin recibe los 3; un Vendedor sólo el suyo,
    /// así el selector de local del frontend no puede ofrecerle algo que no puede consultar.
    /// </summary>
    public async Task<List<LocalDto>> ListarVisiblesAsync(CancellationToken ct = default)
    {
        var query = db.Locales.AsNoTracking().AsQueryable();

        if (usuario.Rol != Rol.Admin)
        {
            var propio = usuario.LocalId ?? -1;
            query = query.Where(l => l.Id == propio);
        }

        return await query
            .OrderBy(l => l.Nombre)
            .Select(l => new LocalDto(l.Id, l.Nombre, l.Direccion))
            .ToListAsync(ct);
    }
}
