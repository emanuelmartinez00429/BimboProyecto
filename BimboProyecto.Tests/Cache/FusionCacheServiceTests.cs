using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaDatos.Cache;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using Xunit;

namespace BimboProyecto.Tests.Cache;

/// <summary>
/// Cubre los comportamientos de la caché que fallan EN SILENCIO: nada de esto
/// rompe la compilación ni lanza excepciones si se implementa mal, simplemente
/// deja de funcionar sin que nadie se entere.
/// </summary>
public class FusionCacheServiceTests
{
    private static (ICacheService cache, IFusionCache fusion) Crear()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var sp = services.BuildServiceProvider();
        var fusion = sp.GetRequiredService<IFusionCache>();
        return (new FusionCacheService(fusion), fusion);
    }

    private static PoliticaCache Politica(TimeSpan? duracion = null) => new(
        Duracion: duracion ?? TimeSpan.FromMinutes(10),
        Jitter:   TimeSpan.Zero,
        MaxViejo: TimeSpan.FromHours(1),
        EsperaEntreReintentos: TimeSpan.FromSeconds(1));

    [Fact(DisplayName = "Un acierto de caché no vuelve a invocar la fábrica")]
    public async Task Acierto_no_reinvoca_la_fabrica()
    {
        var (cache, _) = Crear();
        var invocaciones = 0;

        for (var i = 0; i < 5; i++)
        {
            var r = await cache.ObtenerOCrearAsync<int>(
                "clave", _ => { invocaciones++; return Task.FromResult(Result<int>.Ok(42)); }, Politica());
            Assert.True(r.Success);
            Assert.Equal(42, r.Value);
        }

        Assert.Equal(1, invocaciones);
    }

    [Fact(DisplayName = "Las etiquetas se comparan por igualdad exacta: la raíz sola no alcanza")]
    public async Task Purgar_la_raiz_solo_alcanza_si_la_entrada_la_lleva()
    {
        var (cache, _) = Crear();

        // Entrada registrada SOLO con la etiqueta específica — el error que se quiere evitar.
        await cache.ObtenerOCrearAsync<int>(
            "solo-especifica", _ => Task.FromResult(Result<int>.Ok(1)), Politica(),
            etiquetas: [TagsCache.DeTabla(TagsCache.TablaPaises)]);

        // Entrada registrada como manda TagsCache.DeCatalogo: raíz + específica.
        await cache.ObtenerOCrearAsync<int>(
            "con-raiz", _ => Task.FromResult(Result<int>.Ok(1)), Politica(),
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaPaises));

        cache.InvalidarEtiqueta(TagsCache.CatalogosRaiz);

        var invocacionesSoloEspecifica = 0;
        var invocacionesConRaiz = 0;

        await cache.ObtenerOCrearAsync<int>("solo-especifica",
            _ => { invocacionesSoloEspecifica++; return Task.FromResult(Result<int>.Ok(2)); }, Politica());
        await cache.ObtenerOCrearAsync<int>("con-raiz",
            _ => { invocacionesConRaiz++; return Task.FromResult(Result<int>.Ok(2)); }, Politica());

        // La purga por raíz NO alcanza a la que no la lleva: sigue cacheada.
        Assert.Equal(0, invocacionesSoloEspecifica);
        // La que sí la lleva fue purgada y tuvo que recalcularse.
        Assert.Equal(1, invocacionesConRaiz);
    }

    [Fact(DisplayName = "DeCatalogo registra ambas etiquetas, así que sirven las dos purgas")]
    public async Task DeCatalogo_habilita_purga_granular_y_consolidada()
    {
        foreach (var etiqueta in new[] { TagsCache.CatalogosRaiz, TagsCache.DeTabla(TagsCache.TablaFabricante) })
        {
            var (cache, _) = Crear();
            await cache.ObtenerOCrearAsync<int>(
                "catalogos:fabricante:7", _ => Task.FromResult(Result<int>.Ok(1)), Politica(),
                etiquetas: TagsCache.DeCatalogo(TagsCache.TablaFabricante));

            cache.InvalidarEtiqueta(etiqueta);

            var invocaciones = 0;
            await cache.ObtenerOCrearAsync<int>("catalogos:fabricante:7",
                _ => { invocaciones++; return Task.FromResult(Result<int>.Ok(2)); }, Politica());

            Assert.True(invocaciones == 1, $"la purga por '{etiqueta}' no alcanzó la entrada con alcance");
        }
    }

    [Fact(DisplayName = "Un Result fallido no queda cacheado")]
    public async Task Un_fallo_no_envenena_la_clave()
    {
        var (cache, _) = Crear();

        var rFallo = await cache.ObtenerOCrearAsync<int>(
            "clave", _ => Task.FromResult(Result<int>.Fail("sin red")), Politica());
        Assert.False(rFallo.Success);

        // El siguiente intento debe volver a llamar a la fábrica, no servir el error.
        var rOk = await cache.ObtenerOCrearAsync<int>(
            "clave", _ => Task.FromResult(Result<int>.Ok(7)), Politica());

        Assert.True(rOk.Success);
        Assert.Equal(7, rOk.Value);
    }

    [Fact(DisplayName = "esCacheable en falso entrega el valor pero no lo retiene")]
    public async Task No_se_retiene_lo_que_no_entro_completo()
    {
        var (cache, _) = Crear();
        var invocaciones = 0;

        for (var i = 0; i < 3; i++)
        {
            var r = await cache.ObtenerOCrearAsync<int>(
                "grande",
                _ => { invocaciones++; return Task.FromResult(Result<int>.Ok(999)); },
                Politica(),
                esCacheable: _ => false);

            Assert.True(r.Success);
            Assert.Equal(999, r.Value);
        }

        Assert.Equal(3, invocaciones);
    }

    [Fact(DisplayName = "Single-flight: N llamadas concurrentes ejecutan la fábrica una sola vez")]
    public async Task Llamadas_concurrentes_comparten_una_ejecucion()
    {
        var (cache, _) = Crear();
        var invocaciones = 0;
        var arranque = new TaskCompletionSource();

        var tareas = Enumerable.Range(0, 20).Select(_ => cache.ObtenerOCrearAsync<int>(
            "compartida",
            async _ =>
            {
                Interlocked.Increment(ref invocaciones);
                await arranque.Task;
                return Result<int>.Ok(5);
            },
            Politica())).ToArray();

        arranque.SetResult();
        var resultados = await Task.WhenAll(tareas);

        Assert.All(resultados, r => Assert.Equal(5, r.Value));
        Assert.Equal(1, invocaciones);
    }

    [Fact(DisplayName = "La purga total no revive valores por fail-safe")]
    public async Task Purga_total_no_resucita_datos_de_la_sesion_anterior()
    {
        var (cache, _) = Crear();

        await cache.ObtenerOCrearAsync<int>(
            "de-usuario-a", _ => Task.FromResult(Result<int>.Ok(111)), Politica());

        await cache.LimpiarTodoAsync();

        // Si la fábrica falla y el fail-safe estuviera activo sobre lo purgado,
        // devolvería 111 — el dato del usuario anterior.
        var r = await cache.ObtenerOCrearAsync<int>(
            "de-usuario-a", _ => Task.FromResult(Result<int>.Fail("sin red")), Politica());

        Assert.False(r.Success);
    }

    [Fact(DisplayName = "Invalidar etiqueta de roles purga las entradas de roles registradas")]
    public async Task Invalidar_etiqueta_roles_purga_entradas_cacheadas()
    {
        var (cache, _) = Crear();
        var llamadas = 0;

        Task<Result<int>> Fabrica(CancellationToken _)
        {
            llamadas++;
            return Task.FromResult(Result<int>.Ok(100 + llamadas));
        }

        var r1 = await cache.ObtenerOCrearAsync(
            "catalogos:roles:activos", Fabrica, Politica(),
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaRoles));

        var r2 = await cache.ObtenerOCrearAsync(
            "catalogos:roles:activos", Fabrica, Politica(),
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaRoles));

        Assert.Equal(1, llamadas);
        Assert.Equal(101, r1.Value);
        Assert.Equal(101, r2.Value);

        cache.InvalidarEtiqueta(TagsCache.DeTabla(TagsCache.TablaRoles));

        var r3 = await cache.ObtenerOCrearAsync(
            "catalogos:roles:activos", Fabrica, Politica(),
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaRoles));

        Assert.Equal(2, llamadas);
        Assert.Equal(102, r3.Value);
    }
}
