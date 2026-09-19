using CapaAplicacion.Bitacora;
using CapaAplicacion.Reportes;
using Xunit;

namespace BimboProyecto.Tests.Bitacora;

public sealed class TextoAuditoriaTests
{
    [Theory]
    [InlineData("Recepción abierta", "Recepción abierta")]
    [InlineData("Producto: Café; bruto: 20 kg", "Producto: Café; bruto: 20 kg")]
    [InlineData("", "")]
    public void TextoExistenteSeConserva(string entrada, string esperado) =>
        Assert.Equal(esperado, TextoAuditoria.Formatear(entrada));

    [Fact]
    public void ProductoHistoricoTieneNombreEstadoYValoresLegibles()
    {
        const string original = "{\"id_estado\":1,\"nombre_producto\":\"Café\",\"peso_tara\":0.25,\"contenido\":null,\"busqueda_producto\":\"cafe\"}";
        var texto = TextoAuditoria.Formatear(original, "productos");
        Assert.StartsWith("Producto: Café", texto);
        Assert.Contains("Estado: Activo", texto);
        Assert.Contains("Tara (kg): 0.25", texto);
        Assert.Contains("Contenido: Sin dato", texto);
        Assert.DoesNotContain("busqueda_producto", texto);
        Assert.DoesNotContain("{", texto);
        Assert.StartsWith("{", original);
    }

    [Theory]
    [InlineData("movimientos", 7, "Recepción abierta")]
    [InlineData("movimientos", 8, "Recepción cerrada")]
    [InlineData("movimientos", 9, "Recepción anulada")]
    [InlineData("movimiento_productos", 9, "Producto anulado")]
    [InlineData("entradas_producto", 1, "Pesaje activo")]
    [InlineData("entradas_producto", 9, "Pesaje anulado")]
    [InlineData("usuarios", 2, "Inactivo")]
    public void EstadoRespetaLaEntidad(string tabla, int estado, string significado) =>
        Assert.Equal($"Estado: {significado}", TextoAuditoria.Formatear($"{{\"id_estado\":{estado}}}", tabla));

    [Theory]
    [InlineData("{}", "Sin registro")]
    [InlineData("[]", "Ninguno")]
    [InlineData("[1,2,null]", "1, 2, Sin dato")]
    [InlineData("{\"acciones\":[1,2]}", "Referencias de permisos: 1, 2")]
    [InlineData("{\"estado_categoria\":false}", "Estado: Inactiva")]
    [InlineData("{\"es_sistema\":true}", "Rol del sistema: Sí")]
    [InlineData("{\"dato\":{\"valor\":2}}", "Dato: Valor: 2")]
    public void EstructurasSeConviertenSinSintaxisJson(string entrada, string esperado) =>
        Assert.Equal(esperado, TextoAuditoria.Formatear(entrada));

    [Theory]
    [InlineData("{\"nombre\":")]
    [InlineData("[1,")]
    public void HistoricoDanadoNoRompeLaPantallaNiExponeJson(string entrada) =>
        Assert.Equal("Detalle histórico incompleto; consulte el registro original.", TextoAuditoria.Formatear(entrada));

    [Fact]
    public void ParametrosSonTextoConDecimalesEstablesYReferenciasCompletas()
    {
        var texto = ParametrosReporteTexto.Crear(("Origen", "Pesajes"), ("Referencias", new[] { 1, 2 }),
            ("Total neto (kg)", 12.5m), ("Desde", new DateTime(2026, 9, 18)), ("Activo", true));
        Assert.Equal("Origen: Pesajes; Referencias: 1, 2; Total neto (kg): 12.5; Desde: 2026-09-18; Activo: Sí", texto);
    }
}
