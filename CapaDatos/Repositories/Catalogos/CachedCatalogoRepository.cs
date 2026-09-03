using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;
using CapaDatos.Cache;

namespace CapaDatos.Repositories.Catalogos;

/// <summary>
/// Caché de los catálogos chicos, por delante de <see cref="CatalogoRepository"/>.
/// Implementa la misma interfaz, así que ni CapaAplicacion ni CapaUI se enteran de
/// que hay una caché de por medio.
///
/// <para><b>Solo se cachea la apertura</b> —término vacío y página 1—, que es donde
/// están todos los aciertos reales: el selector abre así y después filtra en memoria.
/// Cachear cada tecleo o cada página llenaría la caché de entradas que nadie vuelve
/// a pedir.</para>
///
/// <para><b>Y solo si el catálogo entró completo</b> en esa página. Guardar una vista
/// parcial contaminaría el modo paginado, que es justamente el que se usa cuando la
/// tabla es grande.</para>
///
/// <para>Reemplaza el "mostrar y revalidar" de ADR-015, que revalidaba en CADA
/// apertura: un acierto de caché pagaba el viaje a la red igual. Acá el refresco lo
/// dispara el vencimiento o un evento de Realtime, y las 8 tablas de catálogo están
/// publicadas, así que la red de seguridad está completa.</para>
/// </summary>
public sealed class CachedCatalogoRepository : ICatalogoRepository
{
    private readonly ICatalogoRepository _interno;
    private readonly ICacheService       _cache;

    public CachedCatalogoRepository(ICatalogoRepository interno, ICacheService cache)
        => (_interno, _cache) = (interno, cache);

    // ── Catálogos ultra-estables ─────────────────────────────────────────────

    public Task<Result<PagedResult<FiltroItem>>> GetPaisesAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaPaises, null, PoliticasCache.UltraEstable, termino, page, size,
                 c => _interno.GetPaisesAsync(termino, page, size, c), ct);

    public Task<Result<PagedResult<FiltroItem>>> GetTarasAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaTara, null, PoliticasCache.UltraEstable, termino, page, size,
                 c => _interno.GetTarasAsync(termino, page, size, c), ct);

    public Task<Result<PagedResult<FiltroItem>>> GetUnidadesAsync(
        string termino, int page, int size, int? idTipoUnidad = null, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaUnidadMedida, idTipoUnidad, PoliticasCache.UltraEstable, termino, page, size,
                 c => _interno.GetUnidadesAsync(termino, page, size, idTipoUnidad, c), ct);

    // ── Catálogos de negocio ─────────────────────────────────────────────────

    public Task<Result<PagedResult<FiltroItem>>> GetCategoriasAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaCategoria, null, PoliticasCache.Negocio, termino, page, size,
                 c => _interno.GetCategoriasAsync(termino, page, size, c), ct);

    public Task<Result<PagedResult<FiltroItem>>> GetPresentacionesAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaPresentacion, null, PoliticasCache.Negocio, termino, page, size,
                 c => _interno.GetPresentacionesAsync(termino, page, size, c), ct);

    public Task<Result<PagedResult<FiltroItem>>> GetProveedoresAsync(
        string termino, int page, int size, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaProveedores, null, PoliticasCache.Negocio, termino, page, size,
                 c => _interno.GetProveedoresAsync(termino, page, size, c), ct);

    public Task<Result<PagedResult<FiltroItem>>> GetFabricantesAsync(
        string termino, int page, int size, int? idProveedor = null, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaFabricante, idProveedor, PoliticasCache.Negocio, termino, page, size,
                 c => _interno.GetFabricantesAsync(termino, page, size, idProveedor, c), ct);

    // ── Catálogo dinámico ────────────────────────────────────────────────────

    public Task<Result<PagedResult<FiltroItem>>> GetProductosAsync(
        string termino, int page, int size, int? idProveedor = null, CancellationToken ct = default) =>
        ConCache(TagsCache.TablaProductos, idProveedor, PoliticasCache.Dinamico, termino, page, size,
                 c => _interno.GetProductosAsync(termino, page, size, idProveedor, c), ct);

    // ── Mecánica común ───────────────────────────────────────────────────────

    private Task<Result<PagedResult<FiltroItem>>> ConCache(
        string tabla, int? alcance, PoliticaCache politica,
        string termino, int page, int size,
        Func<CancellationToken, Task<Result<PagedResult<FiltroItem>>>> cargar,
        CancellationToken ct)
    {
        // Búsqueda con término o página distinta de la primera: derecho al servidor.
        if (page != 1 || !string.IsNullOrEmpty(termino))
            return cargar(ct);

        // El tamaño va EN LA CLAVE. Dos consumidores piden la misma tabla, con la
        // misma página y sin término, pero esperan cosas distintas: la lupa pide el
        // catálogo entero (200) para filtrar en memoria, y el selector con paginación
        // forzada pide una página (50) para su grilla. Sin el tamaño en la clave, el
        // segundo pega en la entrada del primero y recibe 200 filas para una página
        // que declara ser de 50 — la grilla las muestra todas y el conteo de páginas
        // queda calculado sobre un conjunto ya completo.
        var ambito = alcance?.ToString() ?? "todos";
        var clave  = $"{TagsCache.CatalogosRaiz}:{tabla}:{ambito}:{size}";

        return _cache.ObtenerOCrearAsync(
            clave,
            cargar,
            politica,
            // Las dos etiquetas, siempre. La raíz habilita la purga consolidada por
            // reconexión de red; la específica, la purga puntual por evento de esa
            // tabla. Registrando solo una de las dos, la otra purga encuentra cero
            // entradas y devuelve sin error.
            etiquetas: TagsCache.DeCatalogo(tabla),
            // Solo se retiene lo que entró entero en la página pedida.
            esCacheable: pagina => pagina.Total <= size,
            ct: ct);
    }
}
