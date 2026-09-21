using CapaDominio;
using Xunit;

namespace BimboProyecto.Tests.Pesaje;

/// <summary>
/// Cuentas de peso de una pesada. Es donde vivía un error que inflaba el neto: la tara
/// individual —el envase de cada bulto— se restaba <b>una sola vez</b> por pesada en vez
/// de una vez por bulto.
/// <para/>
/// La regla la fijó Fernando con este caso: una carga de 120 kg con 10 kg de tara extra,
/// bultos de 10 kg de contenido y 1 kg de envase, son 10 bultos y 100 kg de neto.
/// </summary>
public sealed class PesoCalculatorTests
{
    // ── El caso que define la regla ──────────────────────────────────────────

    [Fact]
    public void Caso_de_referencia_120kg_con_10_bultos()
    {
        var (bultos, taraTotal, neto) = PesoCalculator.Resolver(
            bruto: 120m, taraExtra: 10m, pesoTeorico: 10m, taraInd: 1m);

        Assert.Equal(10, bultos);       // (120 − 10) / (10 + 1)
        Assert.Equal(20m, taraTotal);   // 10 de extra + 10 envases de 1
        Assert.Equal(100m, neto);       // 120 − 20
    }

    [Fact]
    public void La_formula_vieja_inflaba_el_neto_en_el_caso_de_referencia()
    {
        // Lo que hacía el código anterior: restar la tara individual una sola vez.
        var netoViejo = 120m - 1m - 10m;
        var netoNuevo = PesoCalculator.Resolver(120m, 10m, 10m, 1m).Neto;

        Assert.Equal(109m, netoViejo);
        Assert.Equal(100m, netoNuevo);
        Assert.Equal(9m, netoViejo - netoNuevo);   // 9 kg de producto que no llegaron
    }

    // ── Conteo de bultos ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(120, 10, 10, 1, 10)]    // exacto
    [InlineData(23, 1, 10, 1, 2)]       // 22/11 = 2 exacto
    [InlineData(115, 10, 10, 1, 10)]    // 105/11 = 9,55 → 10
    [InlineData(112, 10, 10, 1, 9)]     // 102/11 = 9,27 → 9
    public void BultosEstimados_redondea_al_entero_mas_cercano(
        decimal bruto, decimal extra, decimal teorico, decimal taraInd, int esperado) =>
        Assert.Equal(esperado, PesoCalculator.BultosEstimados(bruto, extra, teorico, taraInd));

    [Fact]
    public void BultosEstimados_nunca_devuelve_cero_si_hay_carga()
    {
        // Media caja sigue trayendo un envase: redondear a 0 dejaría la tara sin restar.
        Assert.Equal(1, PesoCalculator.BultosEstimados(bruto: 6m, taraExtra: 0m, pesoTeorico: 10m, taraInd: 1m));
    }

    [Theory]
    [InlineData(0, 0)]        // sin carga
    [InlineData(10, 10)]      // toda la carga es tara extra
    [InlineData(5, 10)]       // tara extra mayor que el bruto
    public void BultosEstimados_es_null_sin_peso_de_bultos(decimal bruto, decimal extra) =>
        Assert.Null(PesoCalculator.BultosEstimados(bruto, extra, pesoTeorico: 10m, taraInd: 1m));

    [Fact]
    public void BultosEstimados_es_null_sin_peso_teorico() =>
        Assert.Null(PesoCalculator.BultosEstimados(120m, 10m, pesoTeorico: 0m, taraInd: 1m));

    // ── Alcance de cada tara ─────────────────────────────────────────────────

    [Fact]
    public void La_tara_extra_se_cuenta_una_vez_y_la_individual_una_por_bulto()
    {
        // Es la distinción que el código anterior no hacía.
        Assert.Equal(20m, PesoCalculator.TaraTotal(taraExtra: 10m, taraInd: 1m, bultos: 10));
        Assert.Equal(11m, PesoCalculator.TaraTotal(taraExtra: 10m, taraInd: 1m, bultos: 1));
        Assert.Equal(10m, PesoCalculator.TaraTotal(taraExtra: 10m, taraInd: 0m, bultos: 10));
    }

    [Fact]
    public void Con_exactamente_un_bulto_la_formula_vieja_y_la_nueva_coinciden()
    {
        // Por eso el error pasó desapercibido: las pesadas de prueba eran de un bulto.
        var neto = PesoCalculator.Resolver(bruto: 21m, taraExtra: 10m, pesoTeorico: 10m, taraInd: 1m).Neto;
        Assert.Equal(21m - 1m - 10m, neto);
    }

    // ── Conteo real vs estimación ────────────────────────────────────────────

    [Fact]
    public void El_conteo_del_operario_le_gana_a_la_estimacion()
    {
        // El peso sugiere 10 bultos, pero el operario contó 9: manda lo que contó.
        var (bultos, _, neto) = PesoCalculator.Resolver(
            bruto: 120m, taraExtra: 10m, pesoTeorico: 10m, taraInd: 1m, bultosCapturados: 9);

        Assert.Equal(9, bultos);
        Assert.Equal(101m, neto);   // 120 − (10 + 9)
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Un_conteo_ausente_o_cero_cae_en_la_estimacion(int? capturados)
    {
        var bultos = PesoCalculator.Resolver(120m, 10m, 10m, 1m, capturados).Bultos;
        Assert.Equal(10, bultos);
    }

    [Fact]
    public void Sin_peso_teorico_se_asume_un_bulto()
    {
        // Sin con qué estimar, se conserva el comportamiento histórico: una sola tara.
        var (bultos, _, neto) = PesoCalculator.Resolver(120m, 10m, pesoTeorico: 0m, taraInd: 1m);

        Assert.Equal(1, bultos);
        Assert.Equal(109m, neto);
    }

    // ── Datos reales del catálogo ────────────────────────────────────────────

    [Fact]
    public void Producto_con_envase_chico_desvio_de_gramos()
    {
        // COCOA EN POLVO: peso_teorico 10, peso_tara 0,250.
        var (bultos, _, neto) = PesoCalculator.Resolver(
            bruto: 102.5m, taraExtra: 0m, pesoTeorico: 10m, taraInd: 0.25m);

        Assert.Equal(10, bultos);
        Assert.Equal(100m, neto);
        Assert.Equal(2.25m, (102.5m - 0.25m) - neto);   // lo que inflaba la fórmula vieja
    }

    [Fact]
    public void Producto_con_envase_grande_desvio_de_decenas_de_kilos()
    {
        // FECULA DE MAIZ: peso_teorico 345, peso_tara 23,560.
        var (bultos, _, neto) = PesoCalculator.Resolver(
            bruto: 1474.24m, taraExtra: 0m, pesoTeorico: 345m, taraInd: 23.56m);

        Assert.Equal(4, bultos);
        Assert.Equal(1380m, neto);
        Assert.Equal(70.68m, (1474.24m - 23.56m) - neto);
    }

    // ── Diferencia contra lo manifestado ─────────────────────────────────────

    [Fact]
    public void Diferencia_negativa_es_faltante_y_positiva_excedente()
    {
        Assert.Equal(-20m, PesoCalculator.DiferenciaKg(pesoRecibido: 980m, pesoManifestado: 1000m));
        Assert.Equal(15m,  PesoCalculator.DiferenciaKg(pesoRecibido: 1015m, pesoManifestado: 1000m));
    }

    [Fact]
    public void DiferenciaPct_no_divide_por_cero() =>
        Assert.Equal(0m, PesoCalculator.DiferenciaPct(pesoRecibido: 980m, pesoManifestado: 0m));
}
