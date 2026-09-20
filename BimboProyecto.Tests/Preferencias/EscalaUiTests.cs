using CapaAplicacion.Preferencias;
using Xunit;

namespace BimboProyecto.Tests.Preferencias;

/// <summary>
/// Política del escalado propio: rango, paso y factor sugerido. Es la parte del
/// escalado que se puede probar sin levantar una ventana, y es donde vive el riesgo
/// silencioso — un factor mal alineado no rompe nada visible, solo deja la app en un
/// tamaño que el usuario no eligió.
/// </summary>
public sealed class EscalaUiTests
{
    // ── Ajustar: recorte al rango ────────────────────────────────────────────

    [Theory]
    [InlineData(0.10)]
    [InlineData(0.50)]
    [InlineData(0.69)]
    public void Ajustar_recorta_por_debajo_del_minimo(double factor) =>
        Assert.Equal(EscalaUi.Minimo, EscalaUi.Ajustar(factor));

    [Theory]
    [InlineData(1.31)]
    [InlineData(2.00)]
    [InlineData(99.0)]
    public void Ajustar_recorta_por_encima_del_maximo(double factor) =>
        Assert.Equal(EscalaUi.Maximo, EscalaUi.Ajustar(factor));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ajustar_devuelve_el_neutro_ante_valores_imposibles(double factor) =>
        Assert.Equal(EscalaUi.Normal, EscalaUi.Ajustar(factor));

    // ── Ajustar: alineación al paso ──────────────────────────────────────────

    [Theory]
    [InlineData(0.81, 0.80)]
    [InlineData(0.83, 0.85)]
    [InlineData(0.875, 0.90)]   // el medio paso redondea hacia arriba
    [InlineData(1.02, 1.00)]
    [InlineData(1.23, 1.25)]
    public void Ajustar_alinea_al_paso_mas_cercano(double entrada, double esperado) =>
        Assert.Equal(esperado, EscalaUi.Ajustar(entrada));

    [Fact]
    public void Ajustar_no_arrastra_ruido_de_punto_flotante()
    {
        // 0.05 no es exacto en binario: sin el segundo redondeo, 17 * 0.05 da
        // 0.8500000000000001 y ese número terminaría escrito así en el JSON.
        var ajustado = EscalaUi.Ajustar(0.85);

        Assert.Equal(0.85, ajustado);
        Assert.Equal("0.85", ajustado.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Ajustar_es_idempotente()
    {
        var una = EscalaUi.Ajustar(0.83);
        Assert.Equal(una, EscalaUi.Ajustar(una));
    }

    [Fact]
    public void Ajustar_recorre_todo_el_rango_en_pasos_exactos()
    {
        // Los 13 valores elegibles entre 0.70 y 1.30 tienen que sobrevivir el ajuste
        // sin moverse: son los que el usuario va a ver en el control de la Fase 8.
        for (var f = EscalaUi.Minimo; f <= EscalaUi.Maximo + 0.0001; f += EscalaUi.Paso)
        {
            var redondeado = Math.Round(f, 2);
            Assert.Equal(redondeado, EscalaUi.Ajustar(redondeado));
        }
    }

    // ── Comparaciones ────────────────────────────────────────────────────────

    [Fact]
    public void EsNormal_reconoce_el_neutro_con_ruido()
    {
        Assert.True(EscalaUi.EsNormal(1.0));
        Assert.True(EscalaUi.EsNormal(1.0 + 1e-9));
        Assert.False(EscalaUi.EsNormal(0.95));
    }

    [Fact]
    public void SonIguales_tolera_el_ultimo_bit()
    {
        Assert.True(EscalaUi.SonIguales(0.8, 0.7999999999));
        Assert.False(EscalaUi.SonIguales(0.80, 0.85));
    }

    // ── Sugerencia por escala de Windows ─────────────────────────────────────

    [Theory]
    [InlineData(1.00, 1.00)]
    [InlineData(1.25, 0.95)]
    [InlineData(1.50, 0.85)]
    [InlineData(1.75, 0.80)]
    [InlineData(2.00, 0.75)]
    public void Sugerido_sigue_la_tabla(double escalaWindows, double esperado) =>
        Assert.Equal(esperado, EscalaUi.Sugerido(escalaWindows));

    [Fact]
    public void Sugerido_cae_al_tramo_mas_cercano_para_escalas_intermedias()
    {
        // Windows permite escalas personalizadas (ej. 140 %), que no están en la tabla.
        Assert.Equal(0.85, EscalaUi.Sugerido(1.45));
        Assert.Equal(0.95, EscalaUi.Sugerido(1.30));
    }

    [Fact]
    public void Sugerido_no_se_va_de_rango_con_escalas_extremas()
    {
        Assert.InRange(EscalaUi.Sugerido(3.0), EscalaUi.Minimo, EscalaUi.Maximo);
        Assert.InRange(EscalaUi.Sugerido(0.5), EscalaUi.Minimo, EscalaUi.Maximo);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Sugerido_devuelve_el_neutro_ante_valores_imposibles(double escala) =>
        Assert.Equal(EscalaUi.Normal, EscalaUi.Sugerido(escala));

    // ── Coherencia del rango ─────────────────────────────────────────────────

    [Fact]
    public void El_neutro_esta_dentro_del_rango_y_alineado_al_paso()
    {
        Assert.InRange(EscalaUi.Normal, EscalaUi.Minimo, EscalaUi.Maximo);
        Assert.Equal(EscalaUi.Normal, EscalaUi.Ajustar(EscalaUi.Normal));
    }
}
