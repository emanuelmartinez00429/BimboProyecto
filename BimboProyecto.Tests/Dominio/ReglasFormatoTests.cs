using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Dominio;

/// <summary>
/// <see cref="ReglasFormato"/> es lógica de negocio pura y sin dependencias, así que
/// es lo que sí se puede fijar con tests desde acá: el validador que la aplica
/// (<c>CapaUI/Core/Validacion/ValidadorFormulario.cs</c>) vive en CapaUI, que es
/// WPF <c>net8.0-windows</c> y este proyecto no referencia.
/// <para/>
/// El contrato que más importa fijar es el del <b>vacío</b>: las reglas de formato
/// significan "es opcional, pero si lo llenás tiene que estar bien", y la
/// obligatoriedad se declara aparte en <see cref="ReglaCampo.Obligatorio"/>. Es
/// fácil "arreglar" una de estas funciones para que rechace el vacío y volver
/// obligatorio, sin querer, todo campo opcional que tenga formato.
/// </summary>
public sealed class ReglasFormatoTests
{
    // ── El vacío es válido: es el contrato, no un descuido ──────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_EsValidoEnTodosLosFormatos(string? vacio)
    {
        Assert.True(ReglasFormato.EsCorreo(vacio));
        Assert.True(ReglasFormato.EsRtn(vacio));
        Assert.True(ReglasFormato.EsTelefono(vacio));
    }

    // ── Correo ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fbarahona@bimbo.hn")]
    [InlineData("nombre.apellido@sub.dominio.com")]
    public void Correo_AceptaDireccionesRazonables(string correo) =>
        Assert.True(ReglasFormato.EsCorreo(correo));

    [Theory]
    [InlineData("sin-arroba.com")]          // falta la @
    [InlineData("sin@punto")]               // falta el TLD
    [InlineData("con espacio@bimbo.hn")]    // espacio en el medio
    [InlineData("doble@@bimbo.hn")]
    public void Correo_RechazaLosErroresDeTipeoTipicos(string correo) =>
        Assert.False(ReglasFormato.EsCorreo(correo));

    // ── RTN: 14 dígitos, ignorando guiones y espacios ───────────────────────

    [Theory]
    [InlineData("08019995123456")]
    [InlineData("0801-9995-123456")]
    [InlineData("0801 9995 123456")]
    public void Rtn_AceptaCatorceDigitosConOSinSeparadores(string rtn) =>
        Assert.True(ReglasFormato.EsRtn(rtn));

    [Theory]
    [InlineData("0801999512345")]    // 13
    [InlineData("080199951234567")]  // 15
    public void Rtn_RechazaLargoIncorrecto(string rtn) =>
        Assert.False(ReglasFormato.EsRtn(rtn));

    // ── Teléfono: 8 dígitos, o hasta 15 con código de país (E.164) ──────────

    [Theory]
    [InlineData("22334455")]
    [InlineData("2233-4455")]
    [InlineData("+50422334455")]
    public void Telefono_AceptaHondurenoYE164(string telefono) =>
        Assert.True(ReglasFormato.EsTelefono(telefono));

    [Theory]
    [InlineData("1234567")]            // 7, corto
    [InlineData("1234567890123456")]   // 16, pasa el máximo E.164
    public void Telefono_RechazaFueraDeRango(string telefono) =>
        Assert.False(ReglasFormato.EsTelefono(telefono));

    // ── Largos: usados por ReglaCampo.LargoMaximo ───────────────────────────

    [Fact]
    public void NoExcedeLargo_MideSinEspaciosDeLosBordes()
    {
        Assert.True(ReglasFormato.NoExcedeLargo(new string('x', 50), 50));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('x', 51), 50));
        // El trim es parte de la regla: 50 caracteres más espacios sigue entrando.
        Assert.True(ReglasFormato.NoExcedeLargo("  " + new string('x', 50) + "  ", 50));
    }

    /// <summary>
    /// El largo máximo de código de producto es 50 (<see cref="ReglasProducto.Codigo"/>),
    /// que es justo lo que la revisión QA pedía cubrir.
    /// </summary>
    [Fact]
    public void CodigoDeProducto_RespetaSuLargoMaximoDeclarado()
    {
        Assert.Equal(50, ReglasProducto.Codigo.LargoMaximo);
        Assert.True(ReglasProducto.Codigo.Obligatorio);

        Assert.True(ReglasFormato.NoExcedeLargo(new string('A', 50), ReglasProducto.Codigo.LargoMaximo!.Value));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('A', 51), ReglasProducto.Codigo.LargoMaximo!.Value));
    }
}
