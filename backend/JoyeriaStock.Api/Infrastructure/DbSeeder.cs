using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JoyeriaStock.Api.Infrastructure;

/// <summary>
/// Datos mínimos para que la aplicación se pueda usar apenas arranca: 3 locales,
/// categorías, algunos productos con stock y dos usuarios.
/// </summary>
/// <remarks>
/// Es idempotente: si ya hay locales cargados no hace nada, así que se puede llamar
/// en cada arranque sin duplicar. Las contraseñas de ejemplo se leen de configuración
/// para que en un ambiente real se puedan cambiar sin recompilar.
/// </remarks>
public static class DbSeeder
{
    public static async Task SembrarAsync(
        AppDbContext db,
        IPasswordHasher<Usuario> hasher,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.Locales.AnyAsync(ct))
        {
            logger.LogInformation("La base ya tiene datos: se omite el seed.");
            return;
        }

        logger.LogInformation("Base vacía: cargando datos iniciales.");

        var locales = new List<Local>
        {
            new() { Nombre = "Sucursal Centro",     Direccion = "9 de Julio 120" },
            new() { Nombre = "Sucursal Nueva Córdoba", Direccion = "Independencia 850" },
            new() { Nombre = "Sucursal Shopping",   Direccion = "Av. Rafael Núñez 4500, Local 12" }
        };
        db.Locales.AddRange(locales);

        // El prefijo es la parte fija de los SKU que el sistema genera para la categoría.
        var anillos   = new Categoria { Nombre = "Anillos",  PrefijoSku = "ANI" };
        var collares  = new Categoria { Nombre = "Collares", PrefijoSku = "COL" };
        var pulseras  = new Categoria { Nombre = "Pulseras", PrefijoSku = "PUL" };
        var aros      = new Categoria { Nombre = "Aros",     PrefijoSku = "ARO" };
        db.Categorias.AddRange(anillos, collares, pulseras, aros);

        // Los SKU siguen el mismo formato que genera el sistema: PREFIJO-NNNN correlativo.
        var productos = new List<Producto>
        {
            new() { Sku = "ANI-0001", Nombre = "Anillo de plata 925 liso",      Categoria = anillos,  PrecioBase = 18500m,  UmbralStockBajo = 3 },
            new() { Sku = "ANI-0002", Nombre = "Anillo de oro 18k con circón",  Categoria = anillos,  PrecioBase = 245000m, UmbralStockBajo = 1 },
            new() { Sku = "COL-0001", Nombre = "Collar de plata con dije luna", Categoria = collares, PrecioBase = 32000m,  UmbralStockBajo = 4 },
            new() { Sku = "PUL-0001", Nombre = "Pulsera de acero quirúrgico",   Categoria = pulseras, PrecioBase = 12800m,  UmbralStockBajo = 6 },
            new() { Sku = "ARO-0001", Nombre = "Aros argolla de plata 925",     Categoria = aros,     PrecioBase = 15400m,  UmbralStockBajo = 5 },
            new() { Sku = "ARO-0002", Nombre = "Aros de perla cultivada",       Categoria = aros,     PrecioBase = 47900m,  UmbralStockBajo = 2 }
        };
        db.Productos.AddRange(productos);

        // Hace falta guardar para que locales y productos tengan Id antes de armar el stock.
        await db.SaveChangesAsync(ct);

        // Stock inicial distinto por local, y algunos por debajo del umbral para que la
        // pantalla de stock muestre las marcas de stock bajo desde el arranque.
        var cantidadesPorLocal = new[] { 12, 6, 2 };
        foreach (var (local, indice) in locales.Select((l, i) => (l, i)))
        {
            foreach (var (producto, posicion) in productos.Select((p, i) => (p, i)))
            {
                db.Stocks.Add(new Stock
                {
                    ProductoId = producto.Id,
                    LocalId = local.Id,
                    Cantidad = Math.Max(0, cantidadesPorLocal[indice] - posicion)
                });
            }
        }

        var passwordAdmin = config["Seed:PasswordAdmin"] ?? "Admin123!";
        var passwordVendedor = config["Seed:PasswordVendedor"] ?? "Vendedor123!";

        // La identidad del admin sale de configuración para que, al instalarlo en la
        // computadora de la joyería, el dueño entre con SU mail y SU contraseña en lugar
        // de con un usuario de ejemplo. Todavía no hay ABM de usuarios (ver decisiones.md),
        // así que este es el único camino para crear el primer usuario real.
        var admin = new Usuario
        {
            Email = (config["Seed:EmailAdmin"] ?? "admin@joyeria.local").Trim().ToLowerInvariant(),
            Nombre = config["Seed:NombreAdmin"] ?? "Administradora",
            Rol = Rol.Admin,
            LocalId = null
        };
        admin.PasswordHash = hasher.HashPassword(admin, passwordAdmin);

        var vendedores = locales.Select((local, i) =>
        {
            var usuario = new Usuario
            {
                Email = $"vendedor{i + 1}@joyeria.local",
                Nombre = $"Vendedor/a {local.Nombre}",
                Rol = Rol.Vendedor,
                LocalId = local.Id
            };
            usuario.PasswordHash = hasher.HashPassword(usuario, passwordVendedor);
            return usuario;
        }).ToList();

        db.Usuarios.Add(admin);
        db.Usuarios.AddRange(vendedores);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seed completo: {Locales} locales, {Categorias} categorías, {Productos} productos, " +
            "{Usuarios} usuarios. El administrador es {EmailAdmin}.",
            locales.Count, 4, productos.Count, vendedores.Count + 1, admin.Email);
    }
}
