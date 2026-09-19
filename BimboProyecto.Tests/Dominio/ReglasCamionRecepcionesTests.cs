using CapaDominio.Reglas;
using Xunit;
using R = CapaDominio.Reglas.ReglasCamion.RecepcionClave;

namespace BimboProyecto.Tests.Dominio;

/// <summary>
/// Reglas entre filas de los camiones de Pesaje (R4 cupo, R5 placa + proveedor).
/// La BD aplica lo mismo en <c>trg_validar_recepcion_movimiento</c>; estos tests cubren
/// la versión que ve el operador antes de enviar.
/// </summary>
public sealed class ReglasCamionRecepcionesTests
{
    private static IReadOnlyList<string> Validar(R[] abiertas, R[] propuestas)
        => ReglasCamion.ValidarRecepciones(abiertas, propuestas);

    [Fact]
    public void Normalizar_quita_espacios_y_pasa_a_mayusculas()
        => Assert.Equal("ABC-1", ReglasCamion.NormalizarPlaca("  abc-1 "));

    [Fact]
    public void Normalizar_null_devuelve_vacio()
        => Assert.Equal(string.Empty, ReglasCamion.NormalizarPlaca(null));

    [Fact]
    public void Cupo_exacto_es_valido()
    {
        var nuevas = Enumerable.Range(1, ReglasCamion.MaxRecepcionesAbiertas)
            .Select(i => new R(null, $"P{i}", 1, i)).ToArray();
        Assert.Empty(Validar([], nuevas));
    }

    [Fact]
    public void Pasarse_del_cupo_contando_las_abiertas_da_error()
    {
        R[] abiertas = [new(1, "A", 1), new(2, "B", 1), new(3, "C", 1), new(4, "D", 1), new(5, "E", 1)];
        var errores = Validar(abiertas, [new(null, "F", 1, 6)]);
        Assert.Single(errores);
        Assert.Contains("5 camiones", errores[0]);
    }

    [Fact]
    public void El_cupo_cuenta_filas_no_placas()
    {
        R[] abiertas = [new(1, "1212", 1), new(2, "1212", 2), new(3, "1212", 3), new(4, "1212", 4), new(5, "1212", 5)];
        Assert.NotEmpty(Validar(abiertas, [new(null, "1212", 6, 6)]));
    }

    [Fact]
    public void Misma_placa_con_otro_proveedor_es_valida()
        => Assert.Empty(Validar([new(1, "1212", 1)], [new(null, "1212", 2, 2), new(null, " 1212 ", 3, 3)]));

    [Fact]
    public void Misma_placa_y_proveedor_dentro_del_lote_da_error_con_numero_de_fila()
    {
        var errores = Validar([], [new(null, "1212", 7, 1), new(null, "1212", 7, 3)]);
        Assert.Single(errores);
        Assert.Contains("camión 3", errores[0]);
        Assert.Contains("camión 1", errores[0]);
    }

    [Fact]
    public void Misma_placa_y_proveedor_contra_una_abierta_da_error()
    {
        var errores = Validar([new(10, "1212", 7)], [new(null, "1212", 7, 2)]);
        Assert.Single(errores);
        Assert.Contains("ya tiene una recepción abierta", errores[0]);
    }

    [Fact]
    public void La_comparacion_de_placa_ignora_mayusculas_y_espacios()
        => Assert.NotEmpty(Validar([new(10, "abc-1", 7)], [new(null, " ABC-1", 7, 1)]));

    [Fact]
    public void Una_fila_editada_no_choca_consigo_misma_ni_ocupa_otro_lugar()
    {
        R[] abiertas = [new(1, "A", 1), new(2, "B", 1), new(3, "C", 1), new(4, "D", 1), new(5, "E", 1)];
        Assert.Empty(Validar(abiertas, [new(3, "c", 1, 3)]));
    }

    [Fact]
    public void Editar_hacia_una_clave_ocupada_da_error()
        => Assert.NotEmpty(Validar([new(1, "A", 1), new(2, "B", 1)], [new(2, "A", 1, 2)]));

    [Fact]
    public void Placa_o_proveedor_vacios_no_se_reportan_aca()
        => Assert.Empty(Validar([], [new(null, "", 1, 1), new(null, "", 1, 2), new(null, "X", 0, 3)]));
}
