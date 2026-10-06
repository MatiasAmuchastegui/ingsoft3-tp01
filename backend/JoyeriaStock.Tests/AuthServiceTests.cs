using JoyeriaStock.Api.Application.Services;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Entities;
using JoyeriaStock.Api.Domain.Enums;
using Xunit;

namespace JoyeriaStock.Tests;

/// <summary>
/// Invariante del modelo: un Vendedor necesita local asignado; un Admin no debe tener ninguno.
/// </summary>
/// <remarks>
/// No es una formalidad: un vendedor sin local no puede ver ni registrar nada —el alcance se
/// calcula sobre su local— y un admin con local sugiere que está limitado cuando en realidad
/// opera los tres. Las dos situaciones son datos incoherentes, y por eso se rechazan al crear
/// el usuario y no al usarlo.
/// </remarks>
public class AuthServiceTests
{
    [Fact]
    public void VendedorSinLocalAsignado_EsRechazado()
    {
        var usuario = new Usuario { Rol = Rol.Vendedor, LocalId = null };

        var ex = Assert.Throws<ReglaNegocioException>(
            () => AuthService.ValidarCoherenciaRolLocal(usuario));

        Assert.Contains("local", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdminConLocalAsignado_EsRechazado()
    {
        var usuario = new Usuario { Rol = Rol.Admin, LocalId = 1 };

        Assert.Throws<ReglaNegocioException>(
            () => AuthService.ValidarCoherenciaRolLocal(usuario));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void VendedorConLocalAsignado_EsValido(int localId)
    {
        var usuario = new Usuario { Rol = Rol.Vendedor, LocalId = localId };

        // No lanzar ES el comportamiento esperado.
        AuthService.ValidarCoherenciaRolLocal(usuario);
    }

    [Fact]
    public void AdminSinLocal_EsValido()
    {
        var usuario = new Usuario { Rol = Rol.Admin, LocalId = null };

        AuthService.ValidarCoherenciaRolLocal(usuario);
    }
}
