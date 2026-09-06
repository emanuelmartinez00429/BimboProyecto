using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CapaDatos.Cache;
using Xunit;

namespace BimboProyecto.Tests.Cache;

public sealed class InvalidadorCacheRealtimeTests
{
    private sealed class FalsoRealtimeService : IRealtimeService
    {
        public readonly Dictionary<string, Action<CambioRealtime>> Handlers = new(StringComparer.Ordinal);
        public readonly List<string> Desuscritos = new();

        public Task SuscribirAsync(string tabla, Action<CambioRealtime> handler) => Task.CompletedTask;
        public void Desuscribir(string tabla, Action<CambioRealtime> handler) => Desuscritos.Add(tabla);

        public IDisposable Observar(string tabla, Action<CambioRealtime> handler)
        {
            Handlers[tabla] = handler;
            return new DisposableToken(() =>
            {
                Handlers.Remove(tabla);
                Desuscritos.Add(tabla);
            });
        }

        public Task<IDisposable> ObservarAsync(string tabla, string filtro, Action<CambioRealtime> handler, CancellationToken ct = default) =>
            Task.FromResult(Observar(tabla, handler));

        public Task DesconectarAsync() => Task.CompletedTask;

        private sealed class DisposableToken : IDisposable
        {
            private readonly Action _onDispose;
            public DisposableToken(Action onDispose) => _onDispose = onDispose;
            public void Dispose() => _onDispose();
        }
    }

    private sealed class FalsoCacheService : ICacheService
    {
        public readonly List<string> EtiquetasInvalidadas = new();

        public Task<Result<T>> ObtenerOCrearAsync<T>(
            string clave,
            Func<CancellationToken, Task<Result<T>>> fabrica,
            PoliticaCache politica,
            IEnumerable<string>? etiquetas = null,
            Func<T, bool>? esCacheable = null,
            CancellationToken ct = default) => throw new NotImplementedException();

        public void Invalidar(string clave) { }

        public void InvalidarEtiqueta(string etiqueta) => EtiquetasInvalidadas.Add(etiqueta);

        public Task InvalidarEtiquetaAsync(string etiqueta, CancellationToken ct = default)
        {
            EtiquetasInvalidadas.Add(etiqueta);
            return Task.CompletedTask;
        }

        public Task LimpiarTodoAsync() => Task.CompletedTask;
    }

    private sealed class FalsoConexionMonitor : IConexionMonitor
    {
        public EstadoConexion Estado => EstadoConexion.Conectado;
#pragma warning disable CS0067
        public event EventHandler<EstadoConexion>? EstadoCambiado;
#pragma warning restore CS0067
        public event EventHandler? Reconectado;

        public void Iniciar() { }
        public void Detener() { }

        public void DispararReconectado() => Reconectado?.Invoke(this, EventArgs.Empty);
    }

    [Fact]
    public void Suscribir_RegistraTodasLasTablasDeCatalogo()
    {
        var rt = new FalsoRealtimeService();
        var cache = new FalsoCacheService();
        var mon = new FalsoConexionMonitor();

        using var invalidador = new InvalidadorCacheRealtime(rt, cache, mon);
        invalidador.Suscribir();

        // Debe haberse suscrito a las 10 tablas de catálogo
        Assert.Equal(10, rt.Handlers.Count);
        Assert.Contains("fabricante", rt.Handlers.Keys);
        Assert.Contains("proveedores", rt.Handlers.Keys);
        Assert.Contains("categoria", rt.Handlers.Keys);
        Assert.Contains("presentacion_producto", rt.Handlers.Keys);
        Assert.Contains("productos", rt.Handlers.Keys);
        Assert.Contains("empleados", rt.Handlers.Keys);
        Assert.Contains("roles", rt.Handlers.Keys);
    }

    [Fact]
    public void CambioRealtime_InvalidaEtiquetaCorrespondiente()
    {
        var rt = new FalsoRealtimeService();
        var cache = new FalsoCacheService();
        var mon = new FalsoConexionMonitor();

        using var invalidador = new InvalidadorCacheRealtime(rt, cache, mon);
        invalidador.Suscribir();

        // Simular evento en tabla fabricante
        rt.Handlers["fabricante"](new CambioRealtime("INSERT", 1, 1));

        Assert.Contains(TagsCache.DeTabla(TagsCache.TablaFabricante), cache.EtiquetasInvalidadas);
    }

    [Fact]
    public void Desuscribir_LiberaSuscripciones()
    {
        var rt = new FalsoRealtimeService();
        var cache = new FalsoCacheService();
        var mon = new FalsoConexionMonitor();

        using var invalidador = new InvalidadorCacheRealtime(rt, cache, mon);
        invalidador.Suscribir();
        Assert.Equal(10, rt.Handlers.Count);

        invalidador.Desuscribir();
        Assert.Empty(rt.Handlers);
        Assert.Equal(10, rt.Desuscritos.Count);
    }

    [Fact]
    public void Reconexion_InvalidaCatalogosRaiz()
    {
        var rt = new FalsoRealtimeService();
        var cache = new FalsoCacheService();
        var mon = new FalsoConexionMonitor();

        using var invalidador = new InvalidadorCacheRealtime(rt, cache, mon);
        invalidador.Suscribir();

        mon.DispararReconectado();

        Assert.Contains(TagsCache.CatalogosRaiz, cache.EtiquetasInvalidadas);
    }
}
