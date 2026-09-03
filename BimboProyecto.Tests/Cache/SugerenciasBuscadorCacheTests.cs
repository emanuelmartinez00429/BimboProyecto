using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaDatos.Cache;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace BimboProyecto.Tests.Cache;

public class SugerenciasBuscadorCacheTests
{
    private static (ICacheService cache, IFusionCache fusion) CrearCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var sp = services.BuildServiceProvider();
        var fusion = sp.GetRequiredService<IFusionCache>();
        return (new FusionCacheService(fusion), fusion);
    }

    [Fact(DisplayName = "Sugerencias con mismo término normalizado comparten entrada (acierto de caché)")]
    public async Task Sugerencias_normalizadas_comparten_cache()
    {
        var (cache, _) = CrearCache();
        var invocaciones = 0;

        async Task<Result<IReadOnlyList<string>>> Buscar(string termino)
        {
            var aguja = TextoBusqueda.Normalizar(termino).Trim();
            if (aguja.Length < 3)
                return Result<IReadOnlyList<string>>.Ok([$"res-{aguja}"]);

            var clave = $"sug:{TagsCache.TablaProductos}:{aguja}:todos";

            return await cache.ObtenerOCrearAsync(
                clave,
                _ =>
                {
                    invocaciones++;
                    return Task.FromResult(Result<IReadOnlyList<string>>.Ok(new List<string> { $"prod-{aguja}" }));
                },
                PoliticasCache.Sugerencias,
                etiquetas: TagsCache.DeCatalogo(TagsCache.TablaProductos));
        }

        // 1ª llamada: "Harina" (con mayúscula)
        var r1 = await Buscar("Harina");
        Assert.True(r1.Success);
        Assert.Equal(1, invocaciones);

        // 2ª llamada: "harina" (en minúscula) -> Debe ser un CACHE HIT sin invocar la fábrica
        var r2 = await Buscar("harina");
        Assert.True(r2.Success);
        Assert.Equal(1, invocaciones);

        // 3ª llamada: "Harína " (con tilde y espacio) -> Debe normalizar a "harina" y ser CACHE HIT
        var r3 = await Buscar("Harína ");
        Assert.True(r3.Success);
        Assert.Equal(1, invocaciones);
    }

    [Fact(DisplayName = "Sugerencias con menos de 3 caracteres van directo sin guardarse en caché")]
    public async Task Sugerencias_cortas_no_se_cachean()
    {
        var (cache, _) = CrearCache();
        var invocaciones = 0;

        async Task<Result<IReadOnlyList<string>>> Buscar(string termino)
        {
            var aguja = TextoBusqueda.Normalizar(termino).Trim();
            if (aguja.Length < 3)
            {
                invocaciones++;
                return Result<IReadOnlyList<string>>.Ok([$"corta-{aguja}"]);
            }

            var clave = $"sug:{TagsCache.TablaProductos}:{aguja}:todos";
            return await cache.ObtenerOCrearAsync(
                clave,
                _ =>
                {
                    invocaciones++;
                    return Task.FromResult(Result<IReadOnlyList<string>>.Ok(new List<string> { $"larga-{aguja}" }));
                },
                PoliticasCache.Sugerencias,
                etiquetas: TagsCache.DeCatalogo(TagsCache.TablaProductos));
        }

        // Término de 2 caracteres: se ejecuta cada vez
        await Buscar("ha");
        await Buscar("ha");
        Assert.Equal(2, invocaciones);
    }

    [Fact(DisplayName = "Invalidar etiqueta de tabla purga las sugerencias cacheadas")]
    public async Task Invalidar_etiqueta_purga_sugerencias()
    {
        var (cache, _) = CrearCache();
        var invocaciones = 0;

        async Task<Result<IReadOnlyList<string>>> Buscar(string termino)
        {
            var aguja = TextoBusqueda.Normalizar(termino).Trim();
            var clave = $"sug:{TagsCache.TablaProductos}:{aguja}:todos";

            return await cache.ObtenerOCrearAsync(
                clave,
                _ =>
                {
                    invocaciones++;
                    return Task.FromResult(Result<IReadOnlyList<string>>.Ok(new List<string> { $"prod-{aguja}" }));
                },
                PoliticasCache.Sugerencias,
                etiquetas: TagsCache.DeCatalogo(TagsCache.TablaProductos));
        }

        // Carga inicial
        await Buscar("harina");
        Assert.Equal(1, invocaciones);

        // Hit
        await Buscar("harina");
        Assert.Equal(1, invocaciones);

        // Simulación de evento de Realtime sobre la tabla productos
        cache.InvalidarEtiqueta(TagsCache.DeTabla(TagsCache.TablaProductos));

        // Tras invalidación: la fábrica vuelve a ejecutarse
        await Buscar("harina");
        Assert.Equal(2, invocaciones);
    }
}
