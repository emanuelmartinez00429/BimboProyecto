using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Dominio;

/// <summary>
/// Cuentas y cascada de avisos del panel lateral de <c>PesajeModal</c>. El orden de la
/// cascada es lo que más fácil se rompe: con el bruto vacío el modal tiene que abrir
/// neutro, no con un error rojo.
/// </summary>
public sealed class ReglasPanelPesajeTests
{
    private static readonly double[] Previas = [1538, 1470, 1390];

    // ── Escala ──────────────────────────────────────────────────────────────

    [Fact]
    public void Escala_deja_pesadas_chicas_a_media_altura()
        => Assert.Equal(20, ReglasPanelPesaje.EscalaMaxima([10, 10, 10]));

    [Fact]
    public void Escala_es_el_doble_del_promedio_redondeado()
        => Assert.Equal(3000, ReglasPanelPesaje.EscalaMaxima(Previas));

    [Fact]
    public void Escala_contiene_una_pesada_muy_por_encima_del_resto()
        => Assert.Equal(6600, ReglasPanelPesaje.EscalaMaxima([1500, 1500, 6000])); // 6000 + 10 % de aire

    [Fact]
    public void Escala_no_depende_de_la_magnitud()
        => Assert.Equal(6, ReglasPanelPesaje.EscalaMaxima([3, 3]));

    [Fact]
    public void Escala_sin_datos_no_queda_en_cero()
        => Assert.Equal(100, ReglasPanelPesaje.EscalaMaxima([]));

    // ── Raleo del eje X ─────────────────────────────────────────────────────

    [Fact]
    public void Hasta_diez_nodos_se_muestran_todas_las_etiquetas()
        => Assert.All(Enumerable.Range(0, 10), i => Assert.True(ReglasPanelPesaje.MostrarEtiquetaEjeX(i, 10)));

    [Fact]
    public void Con_muchos_nodos_se_ralea_pero_conserva_primera_y_ultima()
    {
        Assert.True(ReglasPanelPesaje.MostrarEtiquetaEjeX(0, 15));
        Assert.True(ReglasPanelPesaje.MostrarEtiquetaEjeX(14, 15));
        Assert.False(ReglasPanelPesaje.MostrarEtiquetaEjeX(3, 15));
        Assert.True(ReglasPanelPesaje.MostrarEtiquetaEjeX(4, 15));
    }

    // ── Cascada de avisos ───────────────────────────────────────────────────

    [Fact]
    public void Bruto_vacio_es_informativo_no_error()
    {
        var a = Assert.Single(ReglasPanelPesaje.EvaluarAlertas(0, -100, 0, 4398, 5000, Previas));
        Assert.Equal(NivelAlertaPesaje.Info, a.Nivel);
    }

    [Fact]
    public void Neto_no_positivo_es_critico_y_excluye_el_resto()
    {
        var a = Assert.Single(ReglasPanelPesaje.EvaluarAlertas(90, -10, 0, 4398, 5000, Previas));
        Assert.Equal(NivelAlertaPesaje.Critica, a.Nivel);
    }

    [Fact]
    public void Excedente_se_avisa_una_sola_vez()
    {
        var alertas = ReglasPanelPesaje.EvaluarAlertas(1600, 1500, 20, 4398, 5000, Previas);
        Assert.Single(alertas, x => x.Nivel == NivelAlertaPesaje.Critica);
        Assert.Equal("Excedente", alertas[0].Titulo);
    }

    [Fact]
    public void Sin_tara_extra_es_advertencia()
        => Assert.Contains(ReglasPanelPesaje.EvaluarAlertas(1600, 1500, 0, 0, 10000, Previas),
            x => x.Titulo == "Sin tara extra");

    [Fact]
    public void Desvio_atipico_se_mide_contra_el_promedio()
        => Assert.Contains(ReglasPanelPesaje.EvaluarAlertas(700, 600, 10, 0, 10000, Previas),
            x => x.Titulo == "Pesada atípica");

    [Fact]
    public void Sin_historial_suficiente_no_hay_desvio()
        => Assert.DoesNotContain(ReglasPanelPesaje.EvaluarAlertas(700, 600, 10, 0, 10000, [1500]),
            x => x.Titulo == "Pesada atípica");

    [Fact]
    public void Pesada_normal_da_ok_con_neto_y_faltante()
    {
        var a = Assert.Single(ReglasPanelPesaje.EvaluarAlertas(1600, 1500, 10, 1000, 5000, Previas));
        Assert.Equal(NivelAlertaPesaje.Ok, a.Nivel);
        Assert.Equal("Neto 1,500 kg · faltante 2,500 kg", a.Mensaje);
    }
}
