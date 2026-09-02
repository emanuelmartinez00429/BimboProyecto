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
    [InlineData("\t\r\n")]
    public void Vacio_EsValidoEnTodosLosFormatos(string? vacio)
    {
        Assert.True(ReglasFormato.EsCorreo(vacio));
        Assert.True(ReglasFormato.EsRtn(vacio));
        Assert.True(ReglasFormato.EsTelefono(vacio));
    }

    // ── TieneContenido ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\t", false)]
    [InlineData("a", true)]
    [InlineData("  texto  ", true)]
    [InlineData("Bimbo", true)]
    public void TieneContenido_EvaluaPresenciaDeTextoUtil(string? texto, bool esperado) =>
        Assert.Equal(esperado, ReglasFormato.TieneContenido(texto));

    // ── Correo ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("fbarahona@bimbo.hn")]
    [InlineData("nombre.apellido@sub.dominio.com")]
    [InlineData("usuario+tag@empresa.hn")]
    [InlineData("soporte@bimbo-honduras.com.hn")]
    public void Correo_AceptaDireccionesRazonables(string correo) =>
        Assert.True(ReglasFormato.EsCorreo(correo));

    [Theory]
    [InlineData("sin-arroba.com")]          // falta la @
    [InlineData("sin@punto")]               // falta el TLD
    [InlineData("con espacio@bimbo.hn")]    // espacio en el medio
    [InlineData("doble@@bimbo.hn")]         // doble @
    [InlineData("@dominio.com")]            // sin local part
    [InlineData("usuario@")]                // sin dominio
    public void Correo_RechazaLosErroresDeTipeoTipicos(string correo) =>
        Assert.False(ReglasFormato.EsCorreo(correo));

    // ── RTN: 14 dígitos, ignorando guiones y espacios ───────────────────────

    [Theory]
    [InlineData("08019995123456")]
    [InlineData("0801-9995-123456")]
    [InlineData("0801 9995 123456")]
    [InlineData(" 0801-1990-123456 ")]
    public void Rtn_AceptaCatorceDigitosConOSinSeparadores(string rtn) =>
        Assert.True(ReglasFormato.EsRtn(rtn));

    [Theory]
    [InlineData("0801999512345")]        // 13 dígitos (corto)
    [InlineData("080199951234567")]      // 15 dígitos (largo)
    [InlineData("0801-1990-12345")]      // 13 dígitos con guiones
    [InlineData("0801-1990-1234567")]    // 15 dígitos con guiones
    [InlineData("0801ABC1234567")]       // letras dentro
    public void Rtn_RechazaLargoOContenidoIncorrecto(string rtn) =>
        Assert.False(ReglasFormato.EsRtn(rtn));

    // ── Teléfono: 8 dígitos, o hasta 15 con código de país (E.164) ──────────

    [Theory]
    [InlineData("22334455")]
    [InlineData("2233-4455")]
    [InlineData("2233 4455")]
    [InlineData("+50422334455")]
    [InlineData("+504 2233-4455")]
    [InlineData("+123456789012345")] // 15 dígitos (máximo E.164)
    public void Telefono_AceptaHondurenoYE164(string telefono) =>
        Assert.True(ReglasFormato.EsTelefono(telefono));

    [Theory]
    [InlineData("1234567")]            // 7 dígitos (corto)
    [InlineData("1234567890123456")]   // 16 dígitos (pasa el máximo E.164)
    [InlineData("+1234567")]           // 7 dígitos con signo
    [InlineData("telefono-invalido")]  // sin dígitos
    public void Telefono_RechazaFueraDeRango(string telefono) =>
        Assert.False(ReglasFormato.EsTelefono(telefono));

    // ── NoExcedeLargo: Pruebas de Frontera y Comportamiento con Trim ─────────

    [Theory]
    [InlineData(null, 50, true)]
    [InlineData("", 50, true)]
    [InlineData("   ", 50, true)]
    [InlineData("\t\r\n", 50, true)]
    [InlineData(null, 0, true)]
    [InlineData("", 0, true)]
    [InlineData("   ", 0, true)]
    [InlineData("a", 0, false)]
    public void NoExcedeLargo_ValoresNulosYVacios_SiempreSonValidos(string? texto, int largoMaximo, bool esperado) =>
        Assert.Equal(esperado, ReglasFormato.NoExcedeLargo(texto, largoMaximo));

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(72)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public void NoExcedeLargo_FronteraExacta_Y_DesbordePorUno(int largoMaximo)
    {
        var exacto = new string('x', largoMaximo);
        var desborde = new string('x', largoMaximo + 1);
        var corto = new string('x', largoMaximo - 1);

        Assert.True(ReglasFormato.NoExcedeLargo(exacto, largoMaximo), $"Exacto {largoMaximo} debe ser válido.");
        Assert.False(ReglasFormato.NoExcedeLargo(desborde, largoMaximo), $"Desborde {largoMaximo}+1 debe ser inválido.");
        Assert.True(ReglasFormato.NoExcedeLargo(corto, largoMaximo), $"Corto {largoMaximo}-1 debe ser válido.");
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public void NoExcedeLargo_ConEspaciosPerimetrales_AplicaTrim(int largoMaximo)
    {
        var exactoConEspacios = "  " + new string('a', largoMaximo) + "   ";
        var desbordeConEspacios = " \t " + new string('a', largoMaximo + 1) + " \r\n ";

        // El trim limpia los bordes antes de medir
        Assert.True(ReglasFormato.NoExcedeLargo(exactoConEspacios, largoMaximo));
        Assert.False(ReglasFormato.NoExcedeLargo(desbordeConEspacios, largoMaximo));
    }

    [Fact]
    public void NoExcedeLargo_MideSinEspaciosDeLosBordes()
    {
        Assert.True(ReglasFormato.NoExcedeLargo(new string('x', 50), 50));
        Assert.False(ReglasFormato.NoExcedeLargo(new string('x', 51), 50));
        // El trim es parte de la regla: 50 caracteres más espacios sigue entrando.
        Assert.True(ReglasFormato.NoExcedeLargo("  " + new string('x', 50) + "  ", 50));
    }

    [Fact]
    public void NoExcedeLargo_Adversarial_TextosExtremosYCaracteresEspeciales()
    {
        const int tope = 500;
        var textoExtremo = new string('Z', 5000);
        Assert.False(ReglasFormato.NoExcedeLargo(textoExtremo, tope));

        var textoUnicode = "Pan Bimbo Artesano con Chía y Ajonjolí — 100% Trigo Orgánico (Honduras)";
        Assert.True(ReglasFormato.NoExcedeLargo(textoUnicode, 200));

        var textoConEmojis = "🍞 Pan Blanco Bimbo 🥖 Calidad Premium 🥐";
        Assert.True(ReglasFormato.NoExcedeLargo(textoConEmojis, 100));
    }

    // ── TieneLargoMinimo: Pruebas de Frontera ────────────────────────────────

    [Theory]
    [InlineData(null, 6, false)]
    [InlineData(null, 0, true)]
    [InlineData(null, -1, true)]
    [InlineData("", 6, false)]
    [InlineData("", 0, true)]
    [InlineData("", 1, false)]
    public void TieneLargoMinimo_CasosNulosYVacios(string? texto, int largoMinimo, bool esperado) =>
        Assert.Equal(esperado, ReglasFormato.TieneLargoMinimo(texto, largoMinimo));

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void TieneLargoMinimo_FronteraExacta_Y_MenorPorUno(int largoMinimo)
    {
        var exacto = new string('a', largoMinimo);
        var menor = new string('a', largoMinimo - 1);
        var mayor = new string('a', largoMinimo + 1);

        Assert.True(ReglasFormato.TieneLargoMinimo(exacto, largoMinimo), $"Exacto {largoMinimo} debe cumplir.");
        Assert.False(ReglasFormato.TieneLargoMinimo(menor, largoMinimo), $"Menor {largoMinimo}-1 no debe cumplir.");
        Assert.True(ReglasFormato.TieneLargoMinimo(mayor, largoMinimo), $"Mayor {largoMinimo}+1 debe cumplir.");
    }

    [Fact]
    public void TieneLargoMinimo_PasswordBcryptMinimoSeis()
    {
        const int minPassword = 6;
        Assert.False(ReglasFormato.TieneLargoMinimo("12345", minPassword));
        Assert.True(ReglasFormato.TieneLargoMinimo("123456", minPassword));
        Assert.True(ReglasFormato.TieneLargoMinimo("1234567", minPassword));
    }

    // ── Reglas Específicas de Dominio Mapeadas ───────────────────────────────

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

    [Fact]
    public void UsuarioAliasCorreo_RespetaTopeCincuenta()
    {
        Assert.Equal(50, ReglasUsuario.Correo.LargoMaximo);
        Assert.True(ReglasUsuario.Correo.Obligatorio);
        Assert.Equal(FormatoCampo.Correo, ReglasUsuario.Correo.Formato);

        var correoValidoExacto = new string('a', 36) + "@bimbo.hn"; // 36 + 9 = 45 chars <= 50
        Assert.True(ReglasFormato.EsCorreo(correoValidoExacto));
        Assert.True(ReglasFormato.NoExcedeLargo(correoValidoExacto, ReglasUsuario.Correo.LargoMaximo!.Value));

        var correoExcedido = new string('a', 45) + "@bimbo.hn"; // 45 + 9 = 54 chars > 50
        Assert.False(ReglasFormato.NoExcedeLargo(correoExcedido, ReglasUsuario.Correo.LargoMaximo!.Value));
    }
}
