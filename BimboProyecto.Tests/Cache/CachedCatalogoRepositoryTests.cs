using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;
using CapaDatos.Cache;
using CapaDatos.Repositories.Catalogos;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace BimboProyecto.Tests.Cache;

/// <summary>
/// Cubre el contrato de claves del decorador de catálogos. La colisión de claves
/// entre consumidores que piden tamaños distintos es invisible: compila, no lanza,
/// y solo se nota como una grilla que muestra más filas de las que dice su página.
/// </summary>
public class CachedCatalogoRepositoryTests
{
    /// <summary>Devuelve tantas filas como el tamaño pedido y cuenta las llamadas.</summary>
    private sealed class RepoFalso : ICatalogoRepository
    {
        public int Llamadas { get; private set; }
        public int TotalReal { get; init; } = 120;

        private Task<Result<PagedResult<FiltroItem>>> Responder(int size)
        {
            Llamadas++;
            var cuantas = Math.Min(size, TotalReal);
            return Task.FromResult(Result<PagedResult<FiltroItem>>.Ok(new PagedResult<FiltroItem>
            {
                Items = Enumerable.Range(1, cuantas).Select(i => new FiltroItem { Id = i, Nombre = $"n{i}" }).ToList(),
                Total = TotalReal,
            }));
        }

        public Task<Result<PagedResult<FiltroItem>>> GetPaisesAsync(string t, int p, int s, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetPresentacionesAsync(string t, int p, int s, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetTarasAsync(string t, int p, int s, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetCategoriasAsync(string t, int p, int s, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetProveedoresAsync(string t, int p, int s, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetUnidadesAsync(string t, int p, int s, int? id = null, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetProductosAsync(string t, int p, int s, int? id = null, CancellationToken ct = default) => Responder(s);
        public Task<Result<PagedResult<FiltroItem>>> GetFabricantesAsync(string t, int p, int s, int? id = null, CancellationToken ct = default) => Responder(s);
    }

    private static (CachedCatalogoRepository repo, RepoFalso interno) Crear(int totalReal = 120)
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var fusion = services.BuildServiceProvider().GetRequiredService<IFusionCache>();
        var interno = new RepoFalso { TotalReal = totalReal };
        return (new CachedCatalogoRepository(interno, new FusionCacheService(fusion)), interno);
    }

    [Fact(DisplayName = "Tamaños distintos no comparten entrada de caché (P-050)")]
    public async Task Tamanos_distintos_no_colisionan()
    {
        // 120 filas: entra completo en el pedido de 200, pero no en el de 50.
        var (repo, interno) = Crear(totalReal: 120);

        var completo = await repo.GetPaisesAsync(string.Empty, 1, 200);
        Assert.True(completo.Success);
        Assert.Equal(120, completo.Value!.Items.Count);

        // El selector con paginación forzada pide una página de 50. Si compartiera
        // clave con la lupa, recibiría las 120 filas de arriba.
        var pagina = await repo.GetPaisesAsync(string.Empty, 1, 50);
        Assert.True(pagina.Success);
        Assert.Equal(50, pagina.Value!.Items.Count);
        Assert.Equal(120, pagina.Value!.Total);

        Assert.Equal(2, interno.Llamadas);
    }

    [Fact(DisplayName = "Mismo tamaño sí reutiliza la entrada")]
    public async Task Mismo_tamano_es_un_acierto()
    {
        var (repo, interno) = Crear(totalReal: 30);

        for (var i = 0; i < 4; i++)
            Assert.True((await repo.GetCategoriasAsync(string.Empty, 1, 200)).Success);

        Assert.Equal(1, interno.Llamadas);
    }

    [Fact(DisplayName = "Alcances distintos del mismo catálogo no se pisan")]
    public async Task Alcances_distintos_son_entradas_distintas()
    {
        var (repo, interno) = Crear(totalReal: 10);

        await repo.GetFabricantesAsync(string.Empty, 1, 200);
        await repo.GetFabricantesAsync(string.Empty, 1, 200, idProveedor: 7);
        await repo.GetFabricantesAsync(string.Empty, 1, 200, idProveedor: 9);
        await repo.GetFabricantesAsync(string.Empty, 1, 200, idProveedor: 7);   // acierto

        Assert.Equal(3, interno.Llamadas);
    }

    [Fact(DisplayName = "Con término o página distinta de la primera no se toca la caché")]
    public async Task Busqueda_y_paginacion_van_siempre_al_servidor()
    {
        var (repo, interno) = Crear(totalReal: 10);

        await repo.GetProveedoresAsync("bimbo", 1, 200);
        await repo.GetProveedoresAsync("bimbo", 1, 200);
        await repo.GetProveedoresAsync(string.Empty, 2, 200);
        await repo.GetProveedoresAsync(string.Empty, 2, 200);

        Assert.Equal(4, interno.Llamadas);
    }

    [Fact(DisplayName = "Un catálogo que no entró completo no queda retenido")]
    public async Task No_se_cachea_una_vista_parcial()
    {
        // 500 filas contra un pedido de 200: la respuesta es parcial.
        var (repo, interno) = Crear(totalReal: 500);

        await repo.GetProductosAsync(string.Empty, 1, 200);
        await repo.GetProductosAsync(string.Empty, 1, 200);

        Assert.Equal(2, interno.Llamadas);
    }
}
