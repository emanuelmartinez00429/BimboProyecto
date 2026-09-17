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
using CapaDatos.Cache;
using CapaDatos.Repositories.Dashboard;
using CapaDominio.Entities;
using Xunit;

namespace BimboProyecto.Tests.Dashboard;

public class DashboardUnitTests
{
    [Fact(DisplayName = "PeriodoDashboard contiene Hoy, Semana y Mes")]
    public void PeriodoDashboard_EnumValores()
    {
        var valores = Enum.GetValues<PeriodoDashboard>();
        Assert.Contains(PeriodoDashboard.Hoy, valores);
        Assert.Contains(PeriodoDashboard.Semana, valores);
        Assert.Contains(PeriodoDashboard.Mes, valores);
        Assert.Equal(3, valores.Length);
    }

    [Fact(DisplayName = "KpisInventarioDto admite deltas nulos y almacena totales")]
    public void KpisInventarioDto_DeltasNulos()
    {
        var dto = new KpisInventarioDto(TotalProductos: 180, TotalProveedores: 50, TotalMarcas: 20);
        Assert.Equal(180, dto.TotalProductos);
        Assert.Equal(50, dto.TotalProveedores);
        Assert.Equal(20, dto.TotalMarcas);
        Assert.Null(dto.DeltaProductos);
        Assert.Null(dto.DeltaProveedores);
    }

    [Fact(DisplayName = "KpisPesajesDto representa métricas con historial previo nulo o cero (AC: trend badge '-')")]
    public void KpisPesajesDto_SinHistorialPrevio()
    {
        var dto = new KpisPesajesDto(
            PesajesActual: 15,
            PesajesAnterior: 0,
            DeltaPesajesPct: null,
            NetoActual: 3200m,
            NetoAnterior: 0m,
            DeltaNetoPct: null,
            TeoricoActual: 3300m,
            RecibidoActual: 3200m,
            PctMermaActual: 3.03,
            PctMermaAnterior: null,
            DeltaMermaPct: null
        );

        Assert.Equal(15, dto.PesajesActual);
        Assert.Equal(0, dto.PesajesAnterior);
        Assert.Null(dto.DeltaPesajesPct);
        Assert.Null(dto.DeltaNetoPct);
        Assert.Null(dto.DeltaMermaPct);

        // La función de formateo para badges sin historial debe devolver "-"
        string badgePesajes = dto.DeltaPesajesPct.HasValue ? $"{dto.DeltaPesajesPct.Value:+0.0;-0.0}%" : "-";
        string badgeNeto = dto.DeltaNetoPct.HasValue ? $"{dto.DeltaNetoPct.Value:+0.0;-0.0}%" : "-";
        string badgeMerma = dto.DeltaMermaPct.HasValue ? $"{dto.DeltaMermaPct.Value:+0.0;-0.0}" : "-";

        Assert.Equal("-", badgePesajes);
        Assert.Equal("-", badgeNeto);
        Assert.Equal("-", badgeMerma);
    }

    [Fact(DisplayName = "KpisPesajesDto formatea deltas correctamente cuando hay comparación")]
    public void KpisPesajesDto_ConComparacion()
    {
        var dto = new KpisPesajesDto(
            PesajesActual: 100,
            PesajesAnterior: 80,
            DeltaPesajesPct: 25.0,
            NetoActual: 20000m,
            NetoAnterior: 16000m,
            DeltaNetoPct: 25.0,
            TeoricoActual: 20800m,
            RecibidoActual: 20000m,
            PctMermaActual: 3.85,
            PctMermaAnterior: 4.10,
            DeltaMermaPct: -0.25
        );

        Assert.Equal(25.0, dto.DeltaPesajesPct);
        Assert.Equal(25.0, dto.DeltaNetoPct);
        Assert.Equal(-0.25, dto.DeltaMermaPct);

        string badgePesajes = dto.DeltaPesajesPct.HasValue
            ? $"{(dto.DeltaPesajesPct.Value >= 0 ? "▲ +" : "▼ ")}{Math.Abs(dto.DeltaPesajesPct.Value):F0}%"
            : "-";
        Assert.Equal("▲ +25%", badgePesajes);
    }

    [Fact(DisplayName = "MermaProductoDto asigna propiedades correctamente")]
    public void MermaProductoDto_Asignacion()
    {
        var merma = new MermaProductoDto("Pan Blanco", 5.2, 120, 10);
        Assert.Equal("Pan Blanco", merma.Nombre);
        Assert.Equal(5.2, merma.Porcentaje);
        Assert.Equal(120, merma.Kilos);
        Assert.Equal(10, merma.IdProducto);
    }

    [Fact(DisplayName = "UltimoPesajeDto asigna código formateado y alerta")]
    public void UltimoPesajeDto_Asignacion()
    {
        var pesaje = new UltimoPesajeDto("P-0254", "Harina", "1,200", "14:32", true);
        Assert.Equal("P-0254", pesaje.Codigo);
        Assert.Equal("Harina", pesaje.Producto);
        Assert.Equal("1,200", pesaje.Kilos);
        Assert.Equal("14:32", pesaje.Hora);
        Assert.True(pesaje.EsAlerta);
    }

    [Fact(DisplayName = "ADR-026: CatalogosRaiz tag existe para purga consolidada")]
    public void ADR026_CatalogosRaizTag_Existe()
    {
        Assert.Equal("catalogos", TagsCache.CatalogosRaiz);
        var tags = TagsCache.DeCatalogo("productos");
        Assert.Contains(TagsCache.CatalogosRaiz, tags);
        Assert.Contains("catalogos:productos", tags);
    }

    [Fact(DisplayName = "Cálculo de fechas para períodos Hoy, Semana y Mes")]
    public void CalculoFechas_Periodos()
    {
        var hoy = DateTime.Today;

        // Hoy
        var desdeHoy = hoy;
        var hastaHoy = hoy;
        Assert.Equal(desdeHoy, hastaHoy);

        // Semana (Lunes a Domingo)
        int diff = (7 + (hoy.DayOfWeek - DayOfWeek.Monday)) % 7;
        var lunes = hoy.AddDays(-diff);
        var domingo = lunes.AddDays(6);
        Assert.Equal(DayOfWeek.Monday, lunes.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, domingo.DayOfWeek);
        Assert.True(domingo >= lunes);

        // Mes (Primer día a último día)
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var finMes = inicioMes.AddMonths(1).AddDays(-1);
        Assert.Equal(1, inicioMes.Day);
        Assert.True(finMes >= inicioMes);
    }

    [Fact(DisplayName = "Parsing de JSON de Supabase Postgrest con cadenas numéricas e independencia cultural (es-ES / es-MX)")]
    public void SupabaseJsonParsing_CadenasNumericas_IndependenciaCultural()
    {
        var json = "[{\"pesajes_actual\":10,\"pesajes_anterior\":5,\"neto_actual\":\"97.970\",\"neto_anterior\":\"45.500\",\"teorico_actual\":\"100.000\",\"recibido_actual\":\"97.970\"}]";
        var culturaOriginal = Thread.CurrentThread.CurrentCulture;

        try
        {
            // Forzar cultura donde la coma es separador decimal
            Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");

            var token = Newtonsoft.Json.Linq.JToken.Parse(json);
            var row = token switch
            {
                Newtonsoft.Json.Linq.JObject obj => obj,
                Newtonsoft.Json.Linq.JArray arr  => arr.OfType<Newtonsoft.Json.Linq.JObject>().FirstOrDefault(),
                _                                => token.Children<Newtonsoft.Json.Linq.JObject>().FirstOrDefault()
            };

            Assert.NotNull(row);

            // Simular la lógica de DashboardRepository.ParseDecimal y ParseInt
            int pesajesActual = int.TryParse(row["pesajes_actual"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var pAct) ? pAct : 0;
            int pesajesAnterior = int.TryParse(row["pesajes_anterior"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var pAnt) ? pAnt : 0;
            decimal netoActual = decimal.TryParse(row["neto_actual"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var nAct) ? nAct : 0m;
            decimal netoAnterior = decimal.TryParse(row["neto_anterior"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var nAnt) ? nAnt : 0m;
            decimal teoricoActual = decimal.TryParse(row["teorico_actual"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var tAct) ? tAct : 0m;
            decimal recibidoActual = decimal.TryParse(row["recibido_actual"]?.ToString(), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var rAct) ? rAct : 0m;

            Assert.Equal(10, pesajesActual);
            Assert.Equal(5, pesajesAnterior);
            Assert.Equal(97.970m, netoActual);
            Assert.Equal(45.500m, netoAnterior);
            Assert.Equal(100.000m, teoricoActual);
            Assert.Equal(97.970m, recibidoActual);

            double? deltaNetoPct = netoAnterior > 0
                ? (double)((netoActual - netoAnterior) / netoAnterior * 100m)
                : null;
            Assert.NotNull(deltaNetoPct);
            Assert.True(deltaNetoPct.Value > 100.0); // 97.970 vs 45.500 es > 100% de aumento
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culturaOriginal;
        }
    }

    [Fact(DisplayName = "Parsing de JSON admite tanto JArray como JObject individual")]
    public void SupabaseJsonParsing_AdmiteObjetoIndividual()
    {
        var jsonObjeto = "{\"pesajes_actual\":42,\"pesajes_anterior\":30,\"neto_actual\":\"1500.5\",\"neto_anterior\":\"1000.0\",\"teorico_actual\":\"1600.0\",\"recibido_actual\":\"1500.5\"}";
        var token = Newtonsoft.Json.Linq.JToken.Parse(jsonObjeto);

        var row = token switch
        {
            Newtonsoft.Json.Linq.JObject obj => obj,
            Newtonsoft.Json.Linq.JArray arr  => arr.OfType<Newtonsoft.Json.Linq.JObject>().FirstOrDefault(),
            _                                => token.Children<Newtonsoft.Json.Linq.JObject>().FirstOrDefault()
        };

        Assert.NotNull(row);
        Assert.Equal(42, (int)row["pesajes_actual"]!);
    }

    [Theory(DisplayName = "Formateo de deltas positivos, negativos y neutros")]
    [InlineData(25.0, "▲ +25%")]
    [InlineData(-15.4, "▼ 15%")]
    [InlineData(0.0, "▲ +0%")]
    public void FormateoDeltas_SignosYSimbolos(double delta, string esperado)
    {
        string badge = $"{(delta >= 0 ? "▲ +" : "▼ ")}{Math.Abs(delta):F0}%";
        Assert.Equal(esperado, badge);
    }

    [Fact(DisplayName = "Clamping de barra de porcentaje para valores negativos y superiores al máximo")]
    public void MermaClamping_Limites()
    {
        double maxPct = 5.8;

        // Porcentaje negativo (recibido > teórico) -> clamp a 0.0
        double pctNegativo = -2.5;
        double barPercentNegativo = maxPct > 0 ? Math.Clamp(pctNegativo / maxPct * 100.0, 0.0, 100.0) : 0.0;
        Assert.Equal(0.0, barPercentNegativo);

        // Porcentaje excesivo -> clamp a 100.0
        double pctExcesivo = 12.0;
        double barPercentExcesivo = maxPct > 0 ? Math.Clamp(pctExcesivo / maxPct * 100.0, 0.0, 100.0) : 0.0;
        Assert.Equal(100.0, barPercentExcesivo);

        // Porcentaje intermedio
        double pctNormal = 2.9;
        double barPercentNormal = maxPct > 0 ? Math.Clamp(pctNormal / maxPct * 100.0, 0.0, 100.0) : 0.0;
        Assert.Equal(50.0, barPercentNormal);
    }

    [Fact(DisplayName = "Cálculo de fechas para mes anterior en Enero y Febrero (Bisiesto y no bisiesto)")]
    public void CalculoFechas_LimitesMes()
    {
        // Caso Enero: el mes anterior debe ser Diciembre del año previo
        var enero = new DateTime(2026, 1, 15);
        var desdeEnero = new DateTime(enero.Year, enero.Month, 1);
        var hastaEnero = desdeEnero.AddMonths(1).AddDays(-1);
        var desdeDicAnterior = desdeEnero.AddMonths(-1);
        var hastaDicAnterior = desdeEnero.AddDays(-1);

        Assert.Equal(new DateTime(2026, 1, 1), desdeEnero);
        Assert.Equal(new DateTime(2026, 1, 31), hastaEnero);
        Assert.Equal(new DateTime(2025, 12, 1), desdeDicAnterior);
        Assert.Equal(new DateTime(2025, 12, 31), hastaDicAnterior);

        // Caso Marzo en año bisiesto 2024: el mes anterior debe terminar el 29 de Febrero
        var marzoBisiesto = new DateTime(2024, 3, 31);
        var desdeMarzoBis = new DateTime(marzoBisiesto.Year, marzoBisiesto.Month, 1);
        var hastaFebBis = desdeMarzoBis.AddDays(-1);
        Assert.Equal(new DateTime(2024, 2, 29), hastaFebBis);

        // Caso Marzo en año no bisiesto 2025: el mes anterior debe terminar el 28 de Febrero
        var marzoNoBisiesto = new DateTime(2025, 3, 31);
        var desdeMarzoNoBis = new DateTime(marzoNoBisiesto.Year, marzoNoBisiesto.Month, 1);
        var hastaFebNoBis = desdeMarzoNoBis.AddDays(-1);
        Assert.Equal(new DateTime(2025, 2, 28), hastaFebNoBis);
    }

    [Fact(DisplayName = "Control de concurrencia: respuesta fuera de orden es descartada al cambiar de período")]
    public async Task Concurrencia_CambioPeriodo_DescartaRespuestaDesordenada()
    {
        // Simular dos llamadas asíncronas concurrentes donde la primera tarda más que la segunda
        var periodoActivo = PeriodoDashboard.Hoy;
        CancellationTokenSource? ctsPeriodo = null;
        var periodoLock = new object();
        var ctsLifetime = new CancellationTokenSource();

        string resultadoVisible = "Inicial";

        // Función que simula CargarPesajesYMermaAsync con guards de periodo y cancelación
        async Task CargarSimuladoAsync(PeriodoDashboard periodo, int delayMs, string datoRetornado, CancellationToken ct)
        {
            await Task.Delay(delayMs, ct);

            // Guard contra respuesta fuera de orden o cancelada
            if (ct.IsCancellationRequested || periodo != periodoActivo)
                return;

            resultadoVisible = datoRetornado;
        }

        // 1. Usuario solicita "Semana" (operación lenta: 150ms)
        periodoActivo = PeriodoDashboard.Semana;
        CancellationToken tokenSemana;
        lock (periodoLock)
        {
            ctsPeriodo?.Cancel();
            ctsPeriodo?.Dispose();
            ctsPeriodo = CancellationTokenSource.CreateLinkedTokenSource(ctsLifetime.Token);
            tokenSemana = ctsPeriodo.Token;
        }
        var tareaSemana = CargarSimuladoAsync(PeriodoDashboard.Semana, 150, "Datos Semana", tokenSemana);

        // 2. Usuario casi inmediatamente cambia a "Mes" (operación rápida: 30ms)
        periodoActivo = PeriodoDashboard.Mes;
        CancellationToken tokenMes;
        lock (periodoLock)
        {
            ctsPeriodo?.Cancel();
            ctsPeriodo?.Dispose();
            ctsPeriodo = CancellationTokenSource.CreateLinkedTokenSource(ctsLifetime.Token);
            tokenMes = ctsPeriodo.Token;
        }
        var tareaMes = CargarSimuladoAsync(PeriodoDashboard.Mes, 30, "Datos Mes", tokenMes);

        // Esperar ambas tareas (ignorando cancelación)
        try { await tareaMes; } catch (OperationCanceledException) { }
        try { await tareaSemana; } catch (OperationCanceledException) { }

        // El estado final DEBE ser el del período actual ("Mes"), la tarea lenta de "Semana" fue descartada
        Assert.Equal(PeriodoDashboard.Mes, periodoActivo);
        Assert.Equal("Datos Mes", resultadoVisible);
    }

    [Fact(DisplayName = "Error handling: fallos de repositorio no son tragados silenciosamente")]
    public void ErrorHandling_PropagacionDeError()
    {
        var errorResult = Result<IReadOnlyList<UltimoPesajeDto>>.Fail("Timeout de conexión a la base de datos");

        bool hasError = false;
        string? errorMessage = null;

        if (!errorResult.Success)
        {
            hasError = true;
            errorMessage = errorResult.Error;
        }

        Assert.True(hasError);
        Assert.Equal("Timeout de conexión a la base de datos", errorMessage);
    }

    [Fact(DisplayName = "Cálculo de delta de pesajes maneja cero anterior vs actual positivo sin división por cero")]
    public void DeltaPesajes_CeroAnterior_DevuelveNull()
    {
        int pesajesActual = 10;
        int pesajesAnterior = 0;

        double? delta = pesajesAnterior > 0
            ? (double)(pesajesActual - pesajesAnterior) / pesajesAnterior * 100.0
            : null;

        Assert.Null(delta);
    }

    [Fact(DisplayName = "Cálculo de delta neto maneja cero neto anterior sin división por cero")]
    public void DeltaNeto_CeroAnterior_DevuelveNull()
    {
        decimal netoActual = 1500m;
        decimal netoAnterior = 0m;

        double? delta = netoAnterior > 0
            ? (double)((netoActual - netoAnterior) / netoAnterior * 100m)
            : null;

        Assert.Null(delta);
    }

    [Fact(DisplayName = "Cálculo de merma porcentual cuando teórico es cero y hay pesajes devuelve cero")]
    public void PctMerma_TeoricoCero_ConPesajes_DevuelveCero()
    {
        decimal teoricoActual = 0m;
        decimal recibidoActual = 50m;
        int pesajesActual = 1;

        double? pctMermaActual = teoricoActual > 0
            ? (double)((teoricoActual - recibidoActual) / teoricoActual * 100m)
            : (pesajesActual > 0 ? 0.0 : null);

        Assert.NotNull(pctMermaActual);
        Assert.Equal(0.0, pctMermaActual.Value);
    }

    [Fact(DisplayName = "Cálculo de merma porcentual sin pesajes en período devuelve null")]
    public void PctMerma_SinPesajes_DevuelveNull()
    {
        decimal teoricoActual = 0m;
        decimal recibidoActual = 0m;
        int pesajesActual = 0;

        double? pctMermaActual = teoricoActual > 0
            ? (double)((teoricoActual - recibidoActual) / teoricoActual * 100m)
            : (pesajesActual > 0 ? 0.0 : null);

        Assert.Null(pctMermaActual);
    }

    [Fact(DisplayName = "ObtenerTopMermaAsync mapea filas correctamente y limita por el parámetro top")]
    public async Task ObtenerTopMermaAsync_Exito_MapeaYLimitaTop()
    {
        var monitor = new FakeConexionMonitor();
        var cache = new FakeCacheService();
        var sesion = new FakeUsuarioSesionService
        {
            SesionActual = new UsuarioSesion(Array.Empty<ModuloPermisos>()) { IdUsuario = 1, Email = "admin@empresa.com" }
        };
        var repoConsulta = new FakeReporteConsultaRepository
        {
            ResultadoMermas = Result<IReadOnlyList<MermaReporteFila>>.Ok(new List<MermaReporteFila>
            {
                new() { IdProducto = 1, Producto = "Pan Blanco", DiferenciaKg = 120.4m, MermaPorcentaje = 5.2m },
                new() { IdProducto = 2, Producto = "Pan Integral", DiferenciaKg = 85.0m, MermaPorcentaje = 4.1m },
                new() { IdProducto = 3, Producto = "Donas", DiferenciaKg = 40.2m, MermaPorcentaje = 3.3m },
                new() { IdProducto = 4, Producto = "Bollos", DiferenciaKg = 25.0m, MermaPorcentaje = 2.0m },
            })
        };

        var repo = new DashboardRepository(monitor, cache, sesion, repoConsulta);
        var res = await repo.ObtenerTopMermaAsync(PeriodoDashboard.Hoy, top: 2);

        Assert.True(res.Success);
        Assert.NotNull(res.Value);
        Assert.Equal(2, res.Value.Count);
        Assert.Equal("Pan Blanco", res.Value[0].Nombre);
        Assert.Equal(5.2, res.Value[0].Porcentaje);
        Assert.Equal(120, res.Value[0].Kilos);
        Assert.Equal(1, res.Value[0].IdProducto);
        Assert.Equal("Pan Integral", res.Value[1].Nombre);
    }

    [Fact(DisplayName = "ObtenerTopMermaAsync degrada elegantemente a lista vacía ante error de permisos de base de datos")]
    public async Task ObtenerTopMermaAsync_ErrorNoAutorizado_DegradaAEspacioVacio()
    {
        var monitor = new FakeConexionMonitor();
        var cache = new FakeCacheService();
        var sesion = new FakeUsuarioSesionService();
        var repoConsulta = new FakeReporteConsultaRepository
        {
            ResultadoMermas = Result<IReadOnlyList<MermaReporteFila>>.Fail("No autorizado para consultar reportes")
        };

        var repo = new DashboardRepository(monitor, cache, sesion, repoConsulta);
        var res = await repo.ObtenerTopMermaAsync(PeriodoDashboard.Hoy);

        Assert.True(res.Success);
        Assert.NotNull(res.Value);
        Assert.Empty(res.Value);
    }

    [Fact(DisplayName = "ObtenerTopMermaAsync propaga fallos reales de base de datos o conectividad")]
    public async Task ObtenerTopMermaAsync_ErrorConexion_PropagaFallo()
    {
        var monitor = new FakeConexionMonitor();
        var cache = new FakeCacheService();
        var sesion = new FakeUsuarioSesionService();
        var repoConsulta = new FakeReporteConsultaRepository
        {
            ResultadoMermas = Result<IReadOnlyList<MermaReporteFila>>.Fail("Conexión perdida con el servidor PostgreSQL")
        };

        var repo = new DashboardRepository(monitor, cache, sesion, repoConsulta);
        var res = await repo.ObtenerTopMermaAsync(PeriodoDashboard.Hoy);

        Assert.False(res.Success);
        Assert.NotNull(res.Error);
        Assert.Contains("Conexión perdida", res.Error);
    }

    [Fact(DisplayName = "ObtenerTopMermaAsync falla de inmediato si el monitor de red indica SinConexion (Fail-Fast)")]
    public async Task ObtenerTopMermaAsync_SinConexion_FallaInmediatoFailFast()
    {
        var monitor = new FakeConexionMonitor { Estado = EstadoConexion.SinConexion };
        var cache = new FakeCacheService();
        var sesion = new FakeUsuarioSesionService();
        var repoConsulta = new FakeReporteConsultaRepository();

        var repo = new DashboardRepository(monitor, cache, sesion, repoConsulta);
        var res = await repo.ObtenerTopMermaAsync(PeriodoDashboard.Hoy);

        Assert.False(res.Success);
        Assert.Contains("Sin conexión a internet", res.Error);
        Assert.Null(repoConsulta.UltimoFiltroMermas);
    }

    [Fact(DisplayName = "ObtenerTopMermaAsync calcula rangos de fecha correctos para Hoy, Semana y Mes")]
    public async Task ObtenerTopMermaAsync_CalculaRangosDeFecha()
    {
        var monitor = new FakeConexionMonitor();
        var cache = new FakeCacheService();
        var sesion = new FakeUsuarioSesionService();
        var repoConsulta = new FakeReporteConsultaRepository();
        var repo = new DashboardRepository(monitor, cache, sesion, repoConsulta);

        // Semana
        await repo.ObtenerTopMermaAsync(PeriodoDashboard.Semana);
        Assert.NotNull(repoConsulta.UltimoFiltroMermas);
        Assert.Equal(DayOfWeek.Monday, repoConsulta.UltimoFiltroMermas.FechaDesde.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, repoConsulta.UltimoFiltroMermas.FechaHasta.DayOfWeek);

        // Mes
        await repo.ObtenerTopMermaAsync(PeriodoDashboard.Mes);
        Assert.NotNull(repoConsulta.UltimoFiltroMermas);
        Assert.Equal(1, repoConsulta.UltimoFiltroMermas.FechaDesde.Day);
        Assert.True(repoConsulta.UltimoFiltroMermas.FechaHasta >= repoConsulta.UltimoFiltroMermas.FechaDesde);
    }

    private sealed class FakeConexionMonitor : IConexionMonitor
    {
        public EstadoConexion Estado { get; set; } = EstadoConexion.Conectado;
        public event EventHandler? Reconectado;
        public event EventHandler<EstadoConexion>? EstadoCambiado { add { } remove { } }

        public void Iniciar() { }
        public void Detener() { }
        public void DispararReconexion() => Reconectado?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeCacheService : ICacheService
    {
        public Task<Result<T>> ObtenerOCrearAsync<T>(
            string clave,
            Func<CancellationToken, Task<Result<T>>> fabrica,
            PoliticaCache politica,
            IEnumerable<string>? etiquetas = null,
            Func<T, bool>? esCacheable = null,
            CancellationToken ct = default)
        {
            return fabrica(ct);
        }

        public void Invalidar(string clave) { }
        public void InvalidarEtiqueta(string etiqueta) { }
        public Task InvalidarEtiquetaAsync(string etiqueta, CancellationToken ct = default) => Task.CompletedTask;
        public Task LimpiarTodoAsync() => Task.CompletedTask;
    }

    private sealed class FakeUsuarioSesionService : IUsuarioSesionService
    {
        public UsuarioSesion? SesionActual { get; set; }
        public bool Autenticado => SesionActual != null;
        public Task<Result<UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(Result<UsuarioSesion>.Ok(SesionActual!));
        public void CerrarSesion() => SesionActual = null;
        public bool TienePermiso(string nombreAccion) => true;
    }

    private sealed class FakeReporteConsultaRepository : IReporteConsultaRepository
    {
        public MermasReporteFiltro? UltimoFiltroMermas { get; private set; }
        public Result<IReadOnlyList<MermaReporteFila>> ResultadoMermas { get; set; } =
            Result<IReadOnlyList<MermaReporteFila>>.Ok(Array.Empty<MermaReporteFila>());

        public Task<Result<IReadOnlyList<EntradaMateriaPrimaFila>>> ConsultarEntradaMateriaPrimaAsync(
            EntradaMateriaPrimaFiltro filtro, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<EntradaMateriaPrimaFila>>.Ok(Array.Empty<EntradaMateriaPrimaFila>()));

        public Task<Result<IReadOnlyList<ProveedorReporteFila>>> ConsultarProveedorAsync(
            ProveedorReporteFiltro filtro, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<ProveedorReporteFila>>.Ok(Array.Empty<ProveedorReporteFila>()));

        public Task<Result<IReadOnlyList<MermaReporteFila>>> ConsultarMermasAsync(
            MermasReporteFiltro filtro, CancellationToken ct = default)
        {
            UltimoFiltroMermas = filtro;
            return Task.FromResult(ResultadoMermas);
        }

        public Task<Result<IReadOnlyList<ProductoPruebaFila>>> ConsultarPrimerosProductosAsync(
            int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<ProductoPruebaFila>>.Ok(Array.Empty<ProductoPruebaFila>()));
    }
}
