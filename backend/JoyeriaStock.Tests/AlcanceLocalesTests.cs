using JoyeriaStock.Api.Application.Abstractions;
using JoyeriaStock.Api.Application.Services;
using JoyeriaStock.Api.Domain;
using JoyeriaStock.Api.Domain.Enums;
using Moq;
using Xunit;

namespace JoyeriaStock.Tests;

/// <summary>
/// Regla de negocio 5: un vendedor sólo ve y opera su propio local; un admin, todos.
/// </summary>
/// <remarks>
/// Éste es el grupo de tests con <b>mock</b>. <see cref="AlcanceLocales"/> depende de
/// <see cref="IUsuarioActual"/>, que es una interfaz y no de <c>HttpContext</c>: por eso la
/// regla se puede probar con un doble, sin levantar un servidor HTTP ni fabricar un token JWT.
///
/// Si la dependencia fuera una clase concreta que lee el request, estos tests necesitarían
/// un servidor entero y dejarían de ser unitarios.
/// </remarks>
public class AlcanceLocalesTests
{
    /// <summary>
    /// Fabrica el doble. Sustituye al usuario real que saldría del token de la petición.
    /// </summary>
    private static Mock<IUsuarioActual> DobleDe(Rol rol, int? localId)
    {
        var doble = new Mock<IUsuarioActual>();
        doble.Setup(u => u.Rol).Returns(rol);
        doble.Setup(u => u.LocalId).Returns(localId);
        return doble;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Lectura
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Vendedor_ConsultandoOtroLocal_EsRechazado()
    {
        var vendedor = DobleDe(Rol.Vendedor, localId: 1).Object;

        Assert.Throws<AccesoDenegadoException>(
            () => AlcanceLocales.ResolverParaLectura(vendedor, localIdPedido: 2));
    }

    /// <summary>
    /// Verifica la INTERACCIÓN con la dependencia y no el valor devuelto: es lo que convierte
    /// al doble en un mock y no en un simple stub.
    /// </summary>
    [Fact]
    public void Vendedor_SinFiltro_VeSuPropioLocalYSeLePreguntaElRol()
    {
        var doble = DobleDe(Rol.Vendedor, localId: 1);

        var resultado = AlcanceLocales.ResolverParaLectura(doble.Object, localIdPedido: null);

        Assert.Equal(1, resultado);
        doble.Verify(u => u.Rol, Times.AtLeastOnce);
        doble.Verify(u => u.LocalId, Times.AtLeastOnce);
    }

    [Fact]
    public void Vendedor_FiltrandoPorSuPropioLocal_LoDeja()
    {
        var vendedor = DobleDe(Rol.Vendedor, localId: 2).Object;

        var resultado = AlcanceLocales.ResolverParaLectura(vendedor, localIdPedido: 2);

        Assert.Equal(2, resultado);
    }

    [Fact]
    public void Admin_SinFiltro_VeTodosLosLocales()
    {
        var admin = DobleDe(Rol.Admin, localId: null).Object;

        var resultado = AlcanceLocales.ResolverParaLectura(admin, localIdPedido: null);

        // null significa "todos los locales", no "ninguno".
        Assert.Null(resultado);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Admin_FiltrandoPorCualquierLocal_LoDeja(int localPedido)
    {
        var admin = DobleDe(Rol.Admin, localId: null).Object;

        var resultado = AlcanceLocales.ResolverParaLectura(admin, localPedido);

        Assert.Equal(localPedido, resultado);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Escritura
    // ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void VerificarEscritura_VendedorEnOtroLocal_EsRechazado()
    {
        var vendedor = DobleDe(Rol.Vendedor, localId: 1).Object;

        Assert.Throws<AccesoDenegadoException>(
            () => AlcanceLocales.VerificarEscritura(vendedor, localId: 3));
    }

    [Fact]
    public void VerificarEscritura_VendedorEnSuPropioLocal_LoDeja()
    {
        var vendedor = DobleDe(Rol.Vendedor, localId: 1).Object;

        AlcanceLocales.VerificarEscritura(vendedor, localId: 1);
    }

    [Fact]
    public void VerificarEscritura_AdminEnCualquierLocal_LoDeja()
    {
        var admin = DobleDe(Rol.Admin, localId: null).Object;

        AlcanceLocales.VerificarEscritura(admin, localId: 3);
    }

    // ─────────────────────────────────────────────────────────────────────
    // El dato incoherente que el modelo no debería permitir, pero igual se defiende
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un vendedor sin local asignado no puede leer ni escribir nada. Es el mismo caso que
    /// <see cref="AuthServiceTests"/> impide al crear el usuario: acá se comprueba que, si
    /// aun así llegara, la regla 5 falla cerrado y no abierto.
    /// </summary>
    [Fact]
    public void VendedorSinLocalAsignado_NoPuedeLeer()
    {
        var roto = DobleDe(Rol.Vendedor, localId: null).Object;

        Assert.Throws<AccesoDenegadoException>(
            () => AlcanceLocales.ResolverParaLectura(roto, localIdPedido: null));
    }

    [Fact]
    public void VendedorSinLocalAsignado_NoPuedeEscribir()
    {
        var roto = DobleDe(Rol.Vendedor, localId: null).Object;

        Assert.Throws<AccesoDenegadoException>(
            () => AlcanceLocales.VerificarEscritura(roto, localId: 1));
    }
}
