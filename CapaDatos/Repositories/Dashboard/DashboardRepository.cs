using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Dashboard;
using CapaAplicacion.Dashboard.Interfaces;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Fabricantes;
using CapaDatos.Modelados.Pesajes;
using CapaDatos.Modelados.Productos;
using Newtonsoft.Json.Linq;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;
using Count = Supabase.Postgrest.Constants.CountType;
using Op = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using CapaDatos;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositories.Dashboard;

public sealed class DashboardRepository : RepositorioBase, IDashboardRepository
{
    private readonly ICacheService _cache;
    private readonly IUsuarioSesionService _sesion;
    private readonly IReporteConsultaRepository _reporteConsultaRepo;

    private static readonly PoliticaCache PoliticaInventario = new(
        Duracion: TimeSpan.FromMinutes(5),
        Jitter: TimeSpan.FromSeconds(30),
        ToleraViejo: true,
        MaxViejo: TimeSpan.FromHours(1),
        EsperaEntreReintentos: TimeSpan.FromSeconds(15)
    );

    public DashboardRepository(
        IConexionMonitor conexion,
        ICacheService cache,
        IUsuarioSesionService sesion,
        IReporteConsultaRepository reporteConsultaRepo)
        : base(conexion)
    {
        _cache = cache;
        _sesion = sesion;
        _reporteConsultaRepo = reporteConsultaRepo;
    }

    public async Task<Result<KpisInventarioDto>> ObtenerKpisInventarioAsync(CancellationToken ct = default)
    {
        return await _cache.ObtenerOCrearAsync(
            "kpis:inventario",
            async token =>
            {
                return await TryAsync(async () =>
                {
                    var client = await ConexionSupabase.GetClientAsync();

                    var prodCount = await client.From<Modelados.Productos.Productos>()
                        .Count(Count.Exact, token);

                    var provCount = await client.From<Modelados.Pesajes.Proveedores>()
                        .Count(Count.Exact, token);

                    var fabCount = await client.From<FabricanteCrud>()
                        .Count(Count.Exact, token);

                    return new KpisInventarioDto(prodCount, provCount, fabCount);
                }, "Obtener KPIs inventario");
            },
            PoliticaInventario,
            etiquetas: [TagsCache.CatalogosRaiz],
            ct: ct);
    }

    public Task<Result<KpisPesajesDto>> ObtenerKpisPesajesAsync(PeriodoDashboard periodo, CancellationToken ct = default)
    {
        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var hoy = DateTime.Today;
            DateTime desdeActual, hastaActual, desdeAnterior, hastaAnterior;

            switch (periodo)
            {
                case PeriodoDashboard.Semana:
                    int diff = (7 + (hoy.DayOfWeek - DayOfWeek.Monday)) % 7;
                    desdeActual = hoy.AddDays(-diff);
                    hastaActual = desdeActual.AddDays(6);
                    desdeAnterior = desdeActual.AddDays(-7);
                    hastaAnterior = hastaActual.AddDays(-7);
                    break;

                case PeriodoDashboard.Mes:
                    desdeActual = new DateTime(hoy.Year, hoy.Month, 1);
                    hastaActual = desdeActual.AddMonths(1).AddDays(-1);
                    desdeAnterior = desdeActual.AddMonths(-1);
                    hastaAnterior = desdeActual.AddDays(-1);
                    break;

                case PeriodoDashboard.Hoy:
                default:
                    desdeActual = hoy;
                    hastaActual = hoy;
                    desdeAnterior = hoy.AddDays(-1);
                    hastaAnterior = hoy.AddDays(-1);
                    break;
            }

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_fecha_desde_actual"]   = desdeActual.ToString("yyyy-MM-dd"),
                ["p_fecha_hasta_actual"]   = hastaActual.ToString("yyyy-MM-dd"),
                ["p_fecha_desde_anterior"] = desdeAnterior.ToString("yyyy-MM-dd"),
                ["p_fecha_hasta_anterior"] = hastaAnterior.ToString("yyyy-MM-dd"),
            };

            var response = await client.Rpc("consultar_kpis_pesajes", parametros);
            ct.ThrowIfCancellationRequested();
            var token = JToken.Parse(response.Content ?? "[]");
            if (token.Type == JTokenType.String)
                token = JToken.Parse(token.Value<string>() ?? "[]");

            var row = token switch
            {
                JObject obj => obj,
                JArray arr  => arr.OfType<JObject>().FirstOrDefault(),
                _           => token.Children<JObject>().FirstOrDefault()
            };

            int pesajesActual     = ParseInt(row?["pesajes_actual"]);
            int pesajesAnterior   = ParseInt(row?["pesajes_anterior"]);
            decimal netoActual    = ParseDecimal(row?["neto_actual"]);
            decimal netoAnterior  = ParseDecimal(row?["neto_anterior"]);
            decimal teoricoActual = ParseDecimal(row?["teorico_actual"]);
            decimal recibidoActual= ParseDecimal(row?["recibido_actual"]);

            double? deltaPesajesPct = pesajesAnterior > 0
                ? (double)(pesajesActual - pesajesAnterior) / pesajesAnterior * 100.0
                : null;

            double? deltaNetoPct = netoAnterior > 0
                ? (double)((netoActual - netoAnterior) / netoAnterior * 100m)
                : null;

            double? pctMermaActual = teoricoActual > 0
                ? (double)((teoricoActual - recibidoActual) / teoricoActual * 100m)
                : (pesajesActual > 0 ? 0.0 : null);

            return new KpisPesajesDto(
                PesajesActual: pesajesActual,
                PesajesAnterior: pesajesAnterior,
                DeltaPesajesPct: deltaPesajesPct,
                NetoActual: netoActual,
                NetoAnterior: netoAnterior,
                DeltaNetoPct: deltaNetoPct,
                TeoricoActual: teoricoActual,
                RecibidoActual: recibidoActual,
                PctMermaActual: pctMermaActual,
                PctMermaAnterior: null,
                DeltaMermaPct: null
            );
        }, "Obtener KPIs pesajes");
    }

    public async Task<Result<IReadOnlyList<MermaProductoDto>>> ObtenerTopMermaAsync(
        PeriodoDashboard periodo,
        int top = 5,
        CancellationToken ct = default)
    {
        return await TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var hoy = DateTime.Today;
            DateTime desde, hasta;

            switch (periodo)
            {
                case PeriodoDashboard.Semana:
                    int diff = (7 + (hoy.DayOfWeek - DayOfWeek.Monday)) % 7;
                    desde = hoy.AddDays(-diff);
                    hasta = desde.AddDays(6);
                    break;
                case PeriodoDashboard.Mes:
                    desde = new DateTime(hoy.Year, hoy.Month, 1);
                    hasta = desde.AddMonths(1).AddDays(-1);
                    break;
                case PeriodoDashboard.Hoy:
                default:
                    desde = hoy;
                    hasta = hoy;
                    break;
            }

            int idUsuario = _sesion.SesionActual?.IdUsuario ?? 0;
            var res = await _reporteConsultaRepo.ConsultarMermasAsync(
                new MermasReporteFiltro(desde, hasta, null, idUsuario), ct);
            ct.ThrowIfCancellationRequested();

            if (!res.Success)
            {
                // Degradación elegante si el usuario no tiene permisos de reportería en base de datos
                if (res.Error?.Contains("autorizado", StringComparison.OrdinalIgnoreCase) == true ||
                    res.Error?.Contains("permiso", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return (IReadOnlyList<MermaProductoDto>)Array.Empty<MermaProductoDto>();
                }

                throw new InvalidOperationException(res.Error ?? "Error al consultar reporte de mermas");
            }

            var items = res.Value!
                .Take(top)
                .Select(m => new MermaProductoDto(
                    Nombre: m.Producto,
                    Porcentaje: (double)(m.MermaPorcentaje ?? 0m),
                    Kilos: (int)Math.Round(m.DiferenciaKg),
                    IdProducto: m.IdProducto
                ))
                .ToList();

            return (IReadOnlyList<MermaProductoDto>)items;
        }, "Obtener top merma");
    }

    public Task<Result<IReadOnlyList<UltimoPesajeDto>>> ObtenerUltimosPesajesAsync(
        int cantidad = 5,
        CancellationToken ct = default)
    {
        return TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();

            var entRes = await client.From<EntradaProducto>()
                .Filter("id_estado", Op.NotEqual, "9")
                .Order("id_pesaje", Ord.Descending)
                .Limit(cantidad)
                .Get(ct);

            var entradas = entRes?.Models ?? new List<EntradaProducto>();
            if (entradas.Count == 0)
                return (IReadOnlyList<UltimoPesajeDto>)Array.Empty<UltimoPesajeDto>();

            var idsProd = entradas.Select(e => e.idProducto).Distinct().ToList();
            var prodsRes = await client.From<Modelados.Productos.Productos>()
                .Filter("id_producto", Op.In, idsProd)
                .Get(ct);

            var dicProds = (prodsRes?.Models ?? new List<Modelados.Productos.Productos>())
                .GroupBy(p => p.idProducto)
                .ToDictionary(g => g.Key, g => (g.First().nombreProducto, g.First().pesoTeorico));

            var resultado = entradas.Select(e =>
            {
                dicProds.TryGetValue(e.idProducto, out var prodInfo);
                string prodNombre = !string.IsNullOrEmpty(prodInfo.nombreProducto) ? prodInfo.nombreProducto : $"Producto #{e.idProducto}";
                bool esAlerta = false;
                if (prodInfo.pesoTeorico.HasValue && prodInfo.pesoTeorico.Value > 0)
                {
                    var dif = Math.Abs(e.pesoNeto - prodInfo.pesoTeorico.Value);
                    esAlerta = (dif / prodInfo.pesoTeorico.Value) >= 0.03m; // Umbral de merma/alerta 3%
                }

                return new UltimoPesajeDto(
                    Codigo: $"P-{e.idPesaje:D4}",
                    Producto: prodNombre,
                    Kilos: e.pesoNeto.ToString("N0", CultureInfo.InvariantCulture),
                    Hora: e.horaEntrada.ToString(@"HH\:mm"),
                    EsAlerta: esAlerta
                );
            }).ToList();

            return (IReadOnlyList<UltimoPesajeDto>)resultado;
        }, "Obtener últimos pesajes");
    }

    private static decimal ParseDecimal(JToken? token)
    {
        if (token is null || token.Type == JTokenType.Null) return 0m;
        if (token.Type is JTokenType.Float or JTokenType.Integer) return token.Value<decimal>();
        var str = token.Value<string>();
        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }

    private static int ParseInt(JToken? token)
    {
        if (token is null || token.Type == JTokenType.Null) return 0;
        if (token.Type == JTokenType.Integer) return token.Value<int>();
        var str = token.Value<string>();
        return int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0;
    }
}
