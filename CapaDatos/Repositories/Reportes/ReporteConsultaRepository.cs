using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositories.Reportes;

public sealed class ReporteConsultaRepository : RepositorioBase, IReporteConsultaRepository
{
    public ReporteConsultaRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<IReadOnlyList<EntradaMateriaPrimaFila>>> ConsultarEntradaMateriaPrimaAsync(EntradaMateriaPrimaFiltro f, CancellationToken ct = default) =>
        ConsultarAsync("consultar_reporte_entrada_materia_prima", new()
        {
            ["p_id_producto"] = f.IdProducto, ["p_id_proveedor"] = f.IdProveedor, ["p_usuario_consultando"] = f.IdUsuario,
        }, x => new EntradaMateriaPrimaFila
        {
            IdPesaje = Int(x, "id_pesaje"), FechaHora = Fecha(x, "fecha_hora"), Producto = Texto(x, "producto"),
            Proveedor = Texto(x, "proveedor"), Placa = Texto(x, "placa"), Pesador = Texto(x, "pesador"),
            PesoBruto = Decimal(x, "peso_bruto"), PesoTara = Decimal(x, "peso_tara"), PesoNeto = Decimal(x, "peso_neto"),
        }, "Consultar entrada de materia prima", ct);

    public Task<Result<IReadOnlyList<ProveedorReporteFila>>> ConsultarProveedorAsync(ProveedorReporteFiltro f, CancellationToken ct = default) =>
        ConsultarAsync("consultar_reporte_por_proveedor", new()
        {
            ["p_id_proveedor"] = f.IdProveedor, ["p_fecha_desde"] = f.FechaDesde.Date,
            ["p_fecha_hasta"] = f.FechaHasta.Date, ["p_usuario_consultando"] = f.IdUsuario,
        }, x => new ProveedorReporteFila
        {
            IdMovimientoProducto = Int(x, "id_mov_producto"), FechaIngreso = Fecha(x, "fecha_ingreso"),
            Producto = Texto(x, "producto"), BultosEstimados = DecimalNulo(x, "bultos_estimados"),
            PesoTeorico = Decimal(x, "peso_teorico"), PesoRecibido = Decimal(x, "peso_recibido"),
            DiferenciaKg = Decimal(x, "diferencia_kg"), DiferenciaMonetaria = Decimal(x, "diferencia_monetaria"),
        }, "Consultar reporte por proveedor", ct);

    public Task<Result<IReadOnlyList<MermaReporteFila>>> ConsultarMermasAsync(MermasReporteFiltro f, CancellationToken ct = default) =>
        ConsultarAsync("consultar_reporte_productos_merma", new()
        {
            ["p_fecha_desde"] = f.FechaDesde.Date, ["p_fecha_hasta"] = f.FechaHasta.Date,
            ["p_id_categoria"] = f.IdCategoria, ["p_usuario_consultando"] = f.IdUsuario,
        }, x => new MermaReporteFila
        {
            IdProducto = Int(x, "id_producto"), Producto = Texto(x, "producto"), Proveedor = Texto(x, "proveedor"),
            Categoria = Texto(x, "categoria"), CantidadEntradas = Int(x, "cantidad_entradas"),
            PesoTeorico = Decimal(x, "peso_teorico"), PesoRecibido = Decimal(x, "peso_recibido"),
            DiferenciaKg = Decimal(x, "diferencia_kg"), MermaPorcentaje = DecimalNulo(x, "merma_porcentaje"),
        }, "Consultar reporte de mermas", ct);

    public Task<Result<IReadOnlyList<ProductoPruebaFila>>> ConsultarPrimerosProductosAsync(int idUsuario, CancellationToken ct = default) =>
        ConsultarAsync("consultar_reporte_primeros_productos", new() { ["p_usuario_consultando"] = idUsuario },
            x => new ProductoPruebaFila
            {
                IdProducto = Int(x, "id_producto"), Codigo = Texto(x, "codigo"), Nombre = Texto(x, "nombre"),
                Categoria = Texto(x, "categoria"), Proveedor = Texto(x, "proveedor"), Estado = Texto(x, "estado"),
            }, "Consultar primeros productos", ct);

    private Task<Result<IReadOnlyList<T>>> ConsultarAsync<T>(string funcion, Dictionary<string, object?> parametros,
        Func<JObject, T> map, string contexto, CancellationToken ct) => TryAsync(async () =>
    {
        ct.ThrowIfCancellationRequested();
        var client = await ConexionSupabase.GetClientAsync();
        var response = await client.Rpc(funcion, parametros);
        var token = JToken.Parse(response.Content ?? "[]");
        if (token.Type == JTokenType.String) token = JToken.Parse(token.Value<string>() ?? "[]");
        return (IReadOnlyList<T>)token.Children<JObject>().Select(map).ToList();
    }, contexto);

    private static string Texto(JObject x, string p) => x[p]?.Value<string>() ?? string.Empty;
    private static int Int(JObject x, string p) => x[p]?.Value<int>() ?? 0;
    private static decimal Decimal(JObject x, string p) => x[p]?.Value<decimal>() ?? 0m;
    private static decimal? DecimalNulo(JObject x, string p) => x[p]?.Type is null or JTokenType.Null ? null : x[p]!.Value<decimal>();
    private static DateTime Fecha(JObject x, string p) => x[p]?.Value<DateTime>() ?? DateTime.MinValue;
}
