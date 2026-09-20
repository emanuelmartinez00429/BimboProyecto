using System.Globalization;
using CapaAplicacion.Preferencias;
using CapaAplicacion.Preferencias.Dtos;
using Xunit;

namespace BimboProyecto.Tests.Preferencias;

/// <summary>
/// El valor de una preferencia viaja como JSON crudo y la clave de la escala lleva la
/// huella de pantalla, que la base valida con un <c>CHECK</c> de formato. Las dos cosas
/// son sensibles a la cultura del equipo: con la cultura en español, un <c>0.8</c> mal
/// formateado se escribe <c>0,8</c> y Postgres rechaza la fila. Estos tests fijan que el
/// formato no dependa de la máquina donde corra la app.
/// </summary>
public sealed class ValorPreferenciaTests
{
    /// <summary>Corre una acción con la cultura fijada, y la restaura pase lo que pase.</summary>
    private static void ConCultura(string cultura, Action prueba)
    {
        var previa = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultura);
            prueba();
        }
        finally
        {
            CultureInfo.CurrentCulture = previa;
        }
    }

    // ── Números ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("es-HN")]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Numero_ida_y_vuelta_no_depende_de_la_cultura(string cultura) =>
        ConCultura(cultura, () =>
        {
            var json = ValorPreferencia.DesdeNumero(0.8);

            Assert.Equal("0.8", json);
            Assert.Equal(0.8, ValorPreferencia.Numero(json, alterno: 1.0));
        });

    [Fact]
    public void DesdeNumero_escribe_punto_decimal_no_coma() =>
        ConCultura("es-HN", () => Assert.Equal("1.25", ValorPreferencia.DesdeNumero(1.25)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es json")]
    [InlineData("\"compacta\"")]
    [InlineData("true")]
    public void Numero_devuelve_el_alterno_cuando_el_json_no_es_numero(string? json) =>
        Assert.Equal(1.0, ValorPreferencia.Numero(json, alterno: 1.0));

    [Fact]
    public void Numero_tolera_un_numero_guardado_como_cadena()
    {
        // Compatibilidad hacia atrás: se acepta al leer, nunca se escribe así.
        Assert.Equal(0.9, ValorPreferencia.Numero("\"0.9\"", alterno: 1.0));
    }

    // ── Enteros ──────────────────────────────────────────────────────────────

    [Fact]
    public void Entero_lee_un_numero_json() =>
        Assert.Equal(25, ValorPreferencia.Entero("25", alterno: 10));

    [Fact]
    public void Entero_devuelve_el_alterno_si_el_json_no_sirve() =>
        Assert.Equal(10, ValorPreferencia.Entero("{}", alterno: 10));

    // ── Texto y booleanos ────────────────────────────────────────────────────

    [Fact]
    public void Texto_ida_y_vuelta_conserva_el_valor()
    {
        var json = ValorPreferencia.DesdeTexto("compacta");
        Assert.Equal("compacta", ValorPreferencia.Texto(json, alterno: "normal"));
    }

    [Fact]
    public void Texto_escapa_comillas_y_acentos()
    {
        var json = ValorPreferencia.DesdeTexto("pantalla \"Pesajes\" — ñandú");
        Assert.Equal("pantalla \"Pesajes\" — ñandú", ValorPreferencia.Texto(json, alterno: ""));
    }

    [Fact]
    public void Texto_devuelve_el_alterno_si_el_json_es_numero() =>
        Assert.Equal("normal", ValorPreferencia.Texto("0.8", alterno: "normal"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Booleano_ida_y_vuelta_conserva_el_valor(bool valor)
    {
        var json = ValorPreferencia.DesdeBooleano(valor);
        Assert.Equal(valor, ValorPreferencia.Booleano(json, alterno: !valor));
    }

    [Fact]
    public void Booleano_devuelve_el_alterno_si_el_json_no_es_booleano() =>
        Assert.True(ValorPreferencia.Booleano("\"true\"", alterno: true));

    // ── Huella de pantalla ───────────────────────────────────────────────────

    [Theory]
    [InlineData("es-HN")]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public void HuellaPantalla_usa_punto_decimal_en_cualquier_cultura(string cultura) =>
        ConCultura(cultura, () =>
            Assert.Equal("1920x1080@1.75", ClavesPreferencia.HuellaPantalla(1920, 1080, 1.75)));

    [Fact]
    public void HuellaPantalla_omite_decimales_cuando_la_escala_es_entera() =>
        Assert.Equal("1366x768@1", ClavesPreferencia.HuellaPantalla(1366, 768, 1.0));

    [Fact]
    public void HuellaPantalla_redondea_el_ruido_de_punto_flotante()
    {
        // 120.0 / 96.0 no da exactamente 1.25 en binario. Sin el redondeo, la misma
        // pantalla generaría dos huellas distintas y la escala se "perdería".
        var huella = ClavesPreferencia.HuellaPantalla(1920, 1080, 120.0 / 96.0);
        Assert.Equal("1920x1080@1.25", huella);
    }

    [Theory]
    [InlineData(1920, 1080, 1.75)]
    [InlineData(1366, 768, 1.0)]
    [InlineData(3840, 2160, 2.0)]
    [InlineData(1280, 1024, 1.5)]
    public void HuellaPantalla_respeta_el_CHECK_de_la_tabla(int ancho, int alto, double escala)
    {
        // Mismo patrón que usuario_preferencias_ambito_formato en la migración
        // 20260920000711_preferencias_usuario.sql.
        var huella = ClavesPreferencia.HuellaPantalla(ancho, alto, escala);

        Assert.Matches(@"^[0-9]{3,5}x[0-9]{3,5}@[0-9]+(\.[0-9]+)?$", huella);
    }

    [Fact]
    public void AmbitoGlobal_es_el_literal_que_espera_la_base() =>
        Assert.Equal("global", ClavesPreferencia.AmbitoGlobal);

    [Theory]
    [InlineData(ClavesPreferencia.EscalaUi)]
    [InlineData(ClavesPreferencia.FilasPorPagina)]
    [InlineData(ClavesPreferencia.DensidadTablas)]
    [InlineData(ClavesPreferencia.PantallaInicio)]
    [InlineData(ClavesPreferencia.RecordarFiltros)]
    public void Las_claves_respetan_el_CHECK_de_la_tabla(string clave) =>
        Assert.Matches("^[a-z][a-z0-9_]{0,63}$", clave);
}
