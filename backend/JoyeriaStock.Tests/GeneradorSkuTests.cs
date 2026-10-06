using JoyeriaStock.Api.Application.Services;
using JoyeriaStock.Api.Domain;
using Xunit;

namespace JoyeriaStock.Tests;

/// <summary>
/// Regla 1: el SKU es único y lo genera el sistema. Acá se prueba la parte pura —la
/// normalización del código que forma el prefijo—, que es donde viven las reglas.
/// </summary>
/// <remarks>
/// Se testea <c>NormalizarCodigo</c> y no <c>GenerarAsync</c> porque la primera es estática
/// y no toca la base: corre en milisegundos y falla señalando exactamente la regla rota.
/// El correlativo de <c>GenerarAsync</c> necesita un DbContext y es un test de integración.
/// </remarks>
public class GeneradorSkuTests
{
    private const int Maximo = 10;

    // ─────────────────────────────────────────────────────────────────────
    // Normalización: "ct", "CT" y " C-T " tienen que ser el mismo código
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ct", "CT")]        // minúsculas
    [InlineData(" Ct ", "CT")]      // espacios alrededor
    [InlineData("c-t", "CT")]       // guión en el medio
    [InlineData("C T", "CT")]       // espacio en el medio
    [InlineData("rel-ct", "RELCT")] // todo junto
    public void NormalizarCodigo_LimpiaEspaciosGuionesYPasaAMayusculas(string entrada, string esperado)
    {
        var resultado = GeneradorSku.NormalizarCodigo(entrada, "Código de línea", Maximo);

        Assert.Equal(esperado, resultado);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Un código sin contenido se rechaza
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]     // se limpia y queda vacío
    [InlineData(" - ")]
    [InlineData(null)]
    public void NormalizarCodigo_SinContenido_EsRechazado(string? entrada)
        => Assert.Throws<ReglaNegocioException>(
            () => GeneradorSku.NormalizarCodigo(entrada, "Código de línea", Maximo));

    // ─────────────────────────────────────────────────────────────────────
    // El largo máximo, y sus dos bordes
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Caso de error: además de rechazar, el mensaje tiene que decir CUÁL es el límite.
    /// Un rechazo que no explica por qué obliga a adivinar, así que el mensaje es
    /// comportamiento y se testea.
    /// </summary>
    [Fact]
    public void NormalizarCodigo_MasLargoQueElMaximo_ExplicaElLimiteEnElMensaje()
    {
        var codigo = new string('A', Maximo + 1);

        var ex = Assert.Throws<ReglaNegocioException>(
            () => GeneradorSku.NormalizarCodigo(codigo, "Código de línea", Maximo));

        Assert.Contains(Maximo.ToString(), ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// El otro borde, y es el que distingue <c>&gt;</c> de <c>&gt;=</c>.
    /// </summary>
    /// <remarks>
    /// Sin este test, cambiar la comparación del largo por <c>&gt;=</c> no rompe nada: el test
    /// de arriba usa un carácter de más y las dos versiones lo rechazan igual. Un código de
    /// exactamente el máximo tiene que ser ACEPTADO, y sólo esto lo comprueba.
    /// </remarks>
    [Fact]
    public void NormalizarCodigo_ExactamenteElMaximo_EsAceptado()
    {
        var codigo = new string('A', Maximo);

        var resultado = GeneradorSku.NormalizarCodigo(codigo, "Código de línea", Maximo);

        Assert.Equal(codigo, resultado);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Sólo letras y números: el SKU se imprime en etiquetas y se dicta por teléfono
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CT!")]
    [InlineData("C.T")]
    [InlineData("Ñ")]      // fuera de A-Z
    [InlineData("CT_1")]
    public void NormalizarCodigo_ConCaracteresQueNoSonLetrasNiNumeros_EsRechazado(string entrada)
        => Assert.Throws<ReglaNegocioException>(
            () => GeneradorSku.NormalizarCodigo(entrada, "Código de línea", Maximo));

    [Fact]
    public void NormalizarCodigo_ConNumeros_EsAceptado()
    {
        var resultado = GeneradorSku.NormalizarCodigo("ct2024", "Código de línea", Maximo);

        Assert.Equal("CT2024", resultado);
    }

    // ─────────────────────────────────────────────────────────────────────
    // La variante opcional: vacío no es un error, es "no hay código de línea"
    // ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizarCodigoOpcional_SinContenido_DevuelveNull(string? entrada)
        => Assert.Null(GeneradorSku.NormalizarCodigoOpcional(entrada, "Código de línea", Maximo));

    [Fact]
    public void NormalizarCodigoOpcional_ConContenido_NormalizaIgualQueElObligatorio()
    {
        var resultado = GeneradorSku.NormalizarCodigoOpcional(" ct ", "Código de línea", Maximo);

        Assert.Equal("CT", resultado);
    }
}
