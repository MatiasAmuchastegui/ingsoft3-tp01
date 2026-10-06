using System.IdentityModel.Tokens.Jwt;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Domain.Enums;
using JoyeriaStock.Api.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace JoyeriaStock.Tests;

/// <summary>
/// Regla 4: el token lleva la identidad y el alcance del usuario, y vence cuando corresponde.
/// </summary>
/// <remarks>
/// Es un unit test y no uno de integración: <see cref="GeneradorTokenJwt"/> recibe sus dos
/// dependencias por constructor —la configuración y el reloj— y ninguna es una base de datos
/// ni una conexión de red. No hace falta levantar la API para probarlo.
///
/// El reloj se reemplaza por un <see cref="FakeTimeProvider"/>: así el vencimiento se puede
/// comprobar fijando la hora en lugar de esperar ocho horas, y el test es determinista — no
/// depende de cuándo se corra. Si el generador usara <c>DateTime.UtcNow</c> esto sería
/// imposible de verificar.
/// </remarks>
public class GeneradorTokenJwtTests
{
    private static readonly DateTimeOffset Momento = new(2026, 3, 15, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Clave de prueba. Mínimo 32 bytes o HMAC-SHA256 no puede firmar.</summary>
    private const string ClaveDePrueba = "clave-solo-para-tests-de-32-bytes-o-mas";

    private static (GeneradorTokenJwt Generador, FakeTimeProvider Reloj) Armar(int minutosDeVida = 480)
    {
        var reloj = new FakeTimeProvider(Momento);
        var opciones = Options.Create(new JwtOptions
        {
            Key = ClaveDePrueba,
            Issuer = "JoyeriaStockTest",
            Audience = "JoyeriaStockTest",
            MinutosDeVida = minutosDeVida,
        });

        return (new GeneradorTokenJwt(opciones, reloj), reloj);
    }

    private static Usuario Vendedor(int localId) => new()
    {
        Id = 7,
        Email = "vendedor1@joyeria.local",
        Nombre = "Vendedora de Centro",
        Rol = Rol.Vendedor,
        LocalId = localId,
    };

    private static Usuario Admin() => new()
    {
        Id = 1,
        Email = "admin@joyeria.local",
        Nombre = "Administradora",
        Rol = Rol.Admin,
        LocalId = null,
    };

    private static JwtSecurityToken Leer(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ─────────────────────────────────────────────────────────────────────
    // Qué viaja adentro del token
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Token_LlevaLaIdentidadDelUsuario()
    {
        var (generador, _) = Armar();

        var (token, _) = generador.Generar(Vendedor(localId: 2));
        var leido = Leer(token);

        Assert.Equal("7", leido.Claims.First(c => c.Type == ClaimsPersonalizados.Sub).Value);
        Assert.Equal("vendedor1@joyeria.local", leido.Claims.First(c => c.Type == ClaimsPersonalizados.Email).Value);
        Assert.Equal("Vendedora de Centro", leido.Claims.First(c => c.Type == ClaimsPersonalizados.Nombre).Value);
    }

    [Theory]
    [InlineData(Rol.Admin)]
    [InlineData(Rol.Vendedor)]
    public void Token_LlevaElRol(Rol rol)
    {
        var (generador, _) = Armar();
        var usuario = rol == Rol.Admin ? Admin() : Vendedor(localId: 1);

        var (token, _) = generador.Generar(usuario);

        Assert.Equal(rol.ToString(), Leer(token).Claims.First(c => c.Type == ClaimsPersonalizados.Rol).Value);
    }

    /// <summary>
    /// El alcance del TP: un vendedor está atado a su local y un admin no.
    /// </summary>
    /// <remarks>
    /// La ausencia del claim es significativa, no un olvido: es lo que
    /// <see cref="JoyeriaStock.Api.Application.Services.AlcanceLocales"/> interpreta como
    /// "este usuario ve todos los locales". Si el generador pusiera el claim en cero para
    /// los admin, el alcance se rompería sin que ningún otro test lo note.
    /// </remarks>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Token_DeUnVendedor_LlevaSuLocal(int localId)
    {
        var (generador, _) = Armar();

        var (token, _) = generador.Generar(Vendedor(localId));

        Assert.Equal(
            localId.ToString(),
            Leer(token).Claims.First(c => c.Type == ClaimsPersonalizados.LocalId).Value);
    }

    [Fact]
    public void Token_DeUnAdmin_NoLlevaClaimDeLocal()
    {
        var (generador, _) = Armar();

        var (token, _) = generador.Generar(Admin());

        Assert.DoesNotContain(Leer(token).Claims, c => c.Type == ClaimsPersonalizados.LocalId);
    }

    // ─────────────────────────────────────────────────────────────────────
    // El vencimiento, con el reloj fijado
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(60)]
    [InlineData(480)]
    [InlineData(1440)]
    public void Token_ExpiraExactamenteALosMinutosConfigurados(int minutos)
    {
        var (generador, _) = Armar(minutosDeVida: minutos);

        var (_, expiraUtc) = generador.Generar(Admin());

        Assert.Equal(Momento.UtcDateTime.AddMinutes(minutos), expiraUtc);
    }

    [Fact]
    public void Token_NoValeAntesDelMomentoEnQueSeEmitio()
    {
        var (generador, _) = Armar();

        var (token, _) = generador.Generar(Admin());
        var leido = Leer(token);

        // El "no antes de" se guarda al segundo, así que se compara sin la fracción.
        Assert.Equal(Momento.UtcDateTime.ToString("s"), leido.ValidFrom.ToString("s"));
    }

    [Fact]
    public void Token_EmitidoMasTarde_VenceMasTarde()
    {
        var (generador, reloj) = Armar(minutosDeVida: 60);

        var (_, primera) = generador.Generar(Admin());
        reloj.Advance(TimeSpan.FromHours(3));
        var (_, segunda) = generador.Generar(Admin());

        // Es lo que comprueba que el generador usa el reloj inyectado y no la hora real.
        Assert.Equal(TimeSpan.FromHours(3), segunda - primera);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Casos de error y de configuración
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Generar_SinUsuario_EsRechazado()
    {
        var (generador, _) = Armar();

        Assert.Throws<ArgumentNullException>(() => generador.Generar(null!));
    }

    [Fact]
    public void Token_LlevaElEmisorYElDestinatarioDeLaConfiguracion()
    {
        var (generador, _) = Armar();

        var (token, _) = generador.Generar(Admin());
        var leido = Leer(token);

        Assert.Equal("JoyeriaStockTest", leido.Issuer);
        Assert.Contains("JoyeriaStockTest", leido.Audiences);
    }
}
