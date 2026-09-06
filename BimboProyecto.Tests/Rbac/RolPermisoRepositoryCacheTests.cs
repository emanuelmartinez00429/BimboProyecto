using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Cache;
using CapaDatos.Repositories.Usuarios;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using Xunit;

namespace BimboProyecto.Tests.Rbac;

public sealed class RolPermisoRepositoryCacheTests
{
#pragma warning disable CS0067
    private sealed class FakeConexionMonitor : IConexionMonitor
    {
        public EstadoConexion Estado { get; set; } = EstadoConexion.Conectado;
        public event EventHandler<EstadoConexion>? EstadoCambiado;
        public event EventHandler? Reconectado;
        public void Iniciar() { }
        public void Detener() { }
    }
#pragma warning restore CS0067

    private sealed class FakeUsuarioSesionService : IUsuarioSesionService
    {
        public CapaDominio.Entities.UsuarioSesion? SesionActual => null;
        public bool Autenticado => true;
        public Task<Result<CapaDominio.Entities.UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public void CerrarSesion() { }
        public bool TienePermiso(string nombreAccion) => true;
    }

    private sealed class SpyCacheService : ICacheService
    {
        public List<string> EtiquetasInvalidadas { get; } = new();
        public bool LanzarExcepcionEnInvalidar { get; set; }

        public Task<Result<T>> ObtenerOCrearAsync<T>(string clave, Func<CancellationToken, Task<Result<T>>> fabrica, PoliticaCache politica, IEnumerable<string>? etiquetas = null, Func<T, bool>? esCacheable = null, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public void Invalidar(string clave) => throw new NotImplementedException();

        public void InvalidarEtiqueta(string etiqueta)
        {
            if (LanzarExcepcionEnInvalidar)
                throw new InvalidOperationException("Fallo simulado en motor de cache");

            EtiquetasInvalidadas.Add(etiqueta);
        }

        public Task InvalidarEtiquetaAsync(string etiqueta, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task LimpiarTodoAsync() =>
            throw new NotImplementedException();
    }

    [Fact(DisplayName = "PurgarCache invoca _cache.InvalidarEtiqueta con TagsCache.RbacDefiniciones")]
    public void PurgarCache_InvocaInvalidarEtiquetaConRbacDefiniciones()
    {
        var spy = new SpyCacheService();
        var repo = new RolPermisoRepository(new FakeConexionMonitor(), new FakeUsuarioSesionService(), spy);

        repo.PurgarCache();

        Assert.Single(spy.EtiquetasInvalidadas);
        Assert.Equal(TagsCache.RbacDefiniciones, spy.EtiquetasInvalidadas[0]);
        Assert.Equal("rbac:definiciones", spy.EtiquetasInvalidadas[0]);
    }

    [Fact(DisplayName = "PurgarCache invalida efectivamente entradas cacheadas bajo RbacDefiniciones en FusionCache")]
    public async Task PurgarCache_InvalidaEfectivamenteEntradaEnFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var sp = services.BuildServiceProvider();
        var fusion = sp.GetRequiredService<IFusionCache>();
        var cache = new FusionCacheService(fusion);

        var repo = new RolPermisoRepository(new FakeConexionMonitor(), new FakeUsuarioSesionService(), cache);

        // Pre-cargar entrada bajo la etiqueta TagsCache.RbacDefiniciones
        var invocaciones = 0;
        Task<Result<string>> Fabrica(CancellationToken _)
        {
            invocaciones++;
            return Task.FromResult(Result<string>.Ok("resultado_permisos"));
        }

        var r1 = await cache.ObtenerOCrearAsync("clave_prueba", Fabrica, PoliticasCache.Rbac, etiquetas: [TagsCache.RbacDefiniciones]);
        Assert.True(r1.Success);
        Assert.Equal(1, invocaciones);

        // Segunda llamada confirma hit en cache
        var r2 = await cache.ObtenerOCrearAsync("clave_prueba", Fabrica, PoliticasCache.Rbac, etiquetas: [TagsCache.RbacDefiniciones]);
        Assert.True(r2.Success);
        Assert.Equal(1, invocaciones);

        // Ejecutar purga de cache desde el repositorio
        repo.PurgarCache();

        // Tercera llamada debe re-invocar la fabrica al haber sido invalidada la etiqueta
        var r3 = await cache.ObtenerOCrearAsync("clave_prueba", Fabrica, PoliticasCache.Rbac, etiquetas: [TagsCache.RbacDefiniciones]);
        Assert.True(r3.Success);
        Assert.Equal(2, invocaciones);
    }

    [Fact(DisplayName = "PurgarCache propaga excepcion si el motor de cache falla (no tiene try/catch interno)")]
    public void PurgarCache_PropagaExcepcionSiCacheFalla()
    {
        var spy = new SpyCacheService { LanzarExcepcionEnInvalidar = true };
        var repo = new RolPermisoRepository(new FakeConexionMonitor(), new FakeUsuarioSesionService(), spy);

        var ex = Assert.Throws<InvalidOperationException>(() => repo.PurgarCache());
        Assert.Equal("Fallo simulado en motor de cache", ex.Message);
    }
}
