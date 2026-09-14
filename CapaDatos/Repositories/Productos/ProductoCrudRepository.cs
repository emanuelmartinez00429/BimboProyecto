using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Cache;
using CapaDatos.Modelados.Productos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op    = Supabase.Postgrest.Constants.Operator;
using Ord   = Supabase.Postgrest.Constants.Ordering;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Productos.Productos>;

namespace CapaDatos.Repositories.Productos;

public class ProductoCrudRepository : RepositorioBase, IProductoRepository
{
    private readonly IUsuarioSesionService _sesionService;
    private readonly ICacheService         _cache;

    public ProductoCrudRepository(
        IConexionMonitor conexion,
        IUsuarioSesionService sesionService,
        ICacheService cache) : base(conexion)
    {
        _sesionService = sesionService;
        _cache         = cache;
    }

    /// <summary>
    /// El embed de fabricante pasa a <c>!inner</c> SOLO cuando se filtra por
    /// proveedor: productos no tiene id_proveedor, así que el filtro viaja por
    /// el recurso anidado y necesita inner join. Dejarlo fijo descartaría en
    /// silencio los productos sin fabricante.
    /// </summary>
    private static string SelectPara(ProductoFiltros filtros)
    {
        string fabricante = filtros.IdProveedor.HasValue
            ? "fabricante!inner(*, proveedores(*))"
            : "fabricante(*, proveedores(*))";

        return $"*, presentacion_producto(*), {fabricante}, categoria(*), paises(*), unidad_medida(*)";
    }

    /// <summary>
    /// Columna y dirección del orden activo. Fuente única: la usan tanto el
    /// listado como el cálculo de "en qué página cae este producto", que de
    /// otro modo se desincronizarían y el buscador saltaría a la página
    /// equivocada.
    /// </summary>
    private static (string columna, Ord direccion) ColumnaOrden(OrdenProducto orden) => orden switch
    {
        OrdenProducto.NombreAsc  => ("nombre_producto", Ord.Ascending),
        OrdenProducto.NombreDesc => ("nombre_producto", Ord.Descending),
        _                        => ("id_producto",     Ord.Ascending),
    };

    private static ProductoDto Map(Modelados.Productos.Productos p) => new()
    {
        Id             = p.idProducto,
        CodigoInterno  = p.codigoProducto    ?? string.Empty,
        Nombre         = p.nombreProducto    ?? string.Empty,
        Contenido      = p.contenidoProducto,
        Presentacion   = p.nombre_Presentacion,
        Fabricante     = p.nombre_Fabricante,
        Proveedor      = p.nombre_Proveedor,
        Categoria      = p.nombre_Categoria,
        Pais           = p.nombre_Pais,
        IdEstado       = p.idEstado,
        IdFabricante   = p.idFabricante,
        IdProveedor    = p.id_Proveedor,
        IdCategoria    = p.idCategoria,
        IdPais         = p.idPais,
        IdPresentacion = p.idPresentacion,
        PesoTeorico    = p.pesoTeorico,
        PesoTara       = p.pesoTara,
        IdUnidad       = p.idUnidad,
        Unidad         = p.abreviatura_Unidad,
        PrecioPorKg    = p.precioPorKg,
        CreatedAt      = p.createdAt?.ToLocalTime(),
        UpdatedAt      = p.updatedAt?.ToLocalTime(),
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(
        int page, int size, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros, ct), "Cargar productos");

    public Task<Result<IReadOnlyList<ProductoDto>>> BuscarSugerenciasAsync(
        string termino, ProductoFiltros filtros, CancellationToken ct = default)
    {
        var aguja = TextoBusqueda.Normalizar(termino).Trim();
        // Menos de 3 caracteres: se consulta en vivo sin cachear (evita inflar memoria con cadenas genéricas).
        if (aguja.Length < 3)
            return TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias");

        var estado = filtros.IdEstado?.ToString() ?? "todos";
        var prov   = filtros.IdProveedor?.ToString() ?? "todos";
        var fab    = filtros.IdFabricante?.ToString() ?? "todos";
        var cat    = filtros.IdCategoria?.ToString() ?? "todos";
        var pais   = filtros.IdPais?.ToString() ?? "todos";
        var clave  = $"sug:{TagsCache.TablaProductos}:{aguja}:{estado}:{prov}:{fab}:{cat}:{pais}";

        return _cache.ObtenerOCrearAsync(
            clave,
            _ => TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias"),
            PoliticasCache.Sugerencias,
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaProductos),
            ct: ct);
    }

    public Task<Result<IReadOnlyList<FiltroItem>>> GetFabricantesAsync(CancellationToken ct = default) =>
        TryAsync(GetFabricantesInternal, "Cargar fabricantes");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetPaisesAsync(CancellationToken ct = default) =>
        TryAsync(GetPaisesInternal, "Cargar países");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetCategoriasAsync(CancellationToken ct = default) =>
        TryAsync(GetCategoriasInternal, "Cargar categorías");

    public Task<Result<int>> GetPaginaDeProductoAsync(
        ProductoDto producto, int size, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeProductoInternal(producto, size, filtros), "Calcular página de producto");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(ProductoDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_codigo_producto"] = dto.CodigoInterno,
                ["p_nombre_producto"] = dto.Nombre,
                ["p_id_presentacion"] = dto.IdPresentacion,
                ["p_id_fabricante"] = dto.IdFabricante,
                ["p_id_unidad"] = dto.IdUnidad,
                ["p_peso_teorico"] = dto.PesoTeorico,
                ["p_peso_tara"] = dto.PesoTara,
                ["p_id_categoria"] = dto.IdCategoria,
                ["p_contenido"] = dto.Contenido,
                ["p_id_pais"] = dto.IdPais,
                ["p_precio_por_kg"] = dto.PrecioPorKg,
                ["p_id_solicitud"] = idSolicitud,
            };

            var response = await client.Rpc("crear_producto_seguro", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "producto", "id_producto");
        }, "Crear producto");

        private static int ObtenerIdCreado(string? json, string entidad, string jsonKey)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"La función devolvió una respuesta vacía para {entidad}.");
        int id;
        try { 
            var token = Newtonsoft.Json.Linq.JToken.Parse(json);
            if (token is Newtonsoft.Json.Linq.JObject obj) {
                if (obj.TryGetValue("message", out var msgToken) || obj.TryGetValue("error", out msgToken))
                    throw new InvalidOperationException(msgToken.Value<string>());
                if (obj.TryGetValue(jsonKey, out var idToken))
                    id = idToken.Value<int>();
                else
                    throw new InvalidOperationException($"La respuesta no contiene el campo {jsonKey}: {json}");
            }
            else {
                id = token.Value<int>();
            }
        }
        catch (Exception ex) when (ex is Newtonsoft.Json.JsonException or FormatException)
        { throw new InvalidOperationException("La función devolvió una respuesta inválida.", ex); }
        
        return id > 0 ? id : throw new InvalidOperationException("La función devolvió un identificador inválido.");
    }

    public Task<Result> UpdateAsync(ProductoDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_producto"] = dto.Id,
                ["p_codigo_producto"] = dto.CodigoInterno,
                ["p_nombre_producto"] = dto.Nombre,
                ["p_id_presentacion"] = dto.IdPresentacion,
                ["p_id_fabricante"] = dto.IdFabricante,
                ["p_id_unidad"] = dto.IdUnidad,
                ["p_peso_teorico"] = dto.PesoTeorico,
                ["p_peso_tara"] = dto.PesoTara,
                ["p_id_categoria"] = dto.IdCategoria,
                ["p_contenido"] = dto.Contenido,
                ["p_id_pais"] = dto.IdPais,
                ["p_precio_por_kg"] = dto.PrecioPorKg,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("actualizar_producto_seguro", parametros);
        }, "Actualizar producto");

    public Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_producto"] = id,
                ["p_id_estado"] = nuevoEstado,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("cambiar_estado_producto_seguro", parametros);
        }, "Cambiar estado de producto");

    public Task<Result> DeleteAsync(int id, Guid idSolicitud, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, EstadoRegistro.Inactivo, idSolicitud, ct);

    private async Task<PagedResult<ProductoDto>> GetPagedInternal(
        int page, int size, ProductoFiltros filtros, CancellationToken ct)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(SelectPara(filtros)), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var filtrosConteo = new ProductoFiltros
        {
            IdFabricante = filtros.IdFabricante,
            IdPais       = filtros.IdPais,
            IdProveedor  = filtros.IdProveedor,
            IdCategoria  = filtros.IdCategoria,
        };

        var (colOrden, dirOrden) = ColumnaOrden(filtros.Orden);

        var pageTask    = query.Order(colOrden, dirOrden).Range(from, to).Get(ct);
        var conteosTask = GetConteosRpcAsync(filtrosConteo, client);
        await Task.WhenAll(pageTask, conteosTask);

        var pageResult = await pageTask;
        var conteos    = await conteosTask;
        var items      = pageResult?.Models.Select(Map).ToList() ?? [];

        return new PagedResult<ProductoDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasInternal(
        string termino, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(SelectPara(filtros)), filtros);

        var aguja = TextoBusqueda.Normalizar(termino);

        var resultado = await query
            .Filter("busqueda_producto", Op.ILike, $"%{aguja}%")
            .Order("nombre_producto",  Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }



    // C12: Consultar catálogo directamente en vez de descargar toda la tabla productos

    private async Task<IReadOnlyList<FiltroItem>> GetFabricantesInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<FabricanteConsulta>()
            .Select("id_fabricante, nombre_fabricante")
            .Order("nombre_fabricante", Ord.Ascending)
            .Get();

        return (resultado?.Models ?? [])
            .Select(f => new FiltroItem { Id = f.idFabricante, Nombre = f.nombreFabricante })
            .ToList();
    }

    private async Task<IReadOnlyList<FiltroItem>> GetPaisesInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<Paises>()
            .Select("id_pais, nombre_pais")
            .Order("nombre_pais", Ord.Ascending)
            .Get();

        return (resultado?.Models ?? [])
            .Select(p => new FiltroItem { Id = p.idPais, Nombre = p.nombrePais })
            .ToList();
    }

    private async Task<IReadOnlyList<FiltroItem>> GetCategoriasInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<Categoria>()
            .Select("id_categoria, nombre_categoria")
            .Order("nombre_categoria", Ord.Ascending)
            .Get();

        return (resultado?.Models ?? [])
            .Select(c => new FiltroItem { Id = c.idCategoria, Nombre = c.nombreCategoria })
            .ToList();
    }

    /// <summary>
    /// Cuenta cuántos productos van ANTES que este con el orden activo. Tiene
    /// que usar la misma columna que el listado (ver <see cref="ColumnaOrden"/>):
    /// contar por id mientras la grilla ordena por nombre manda al usuario a la
    /// página equivocada.
    /// </summary>
    private async Task<int> GetPaginaDeProductoInternal(ProductoDto producto, int size, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var (columna, direccion) = ColumnaOrden(filtros.Orden);

        // "Antes que" se invierte con el orden descendente.
        var comparador = direccion == Ord.Ascending ? Op.LessThan : Op.GreaterThan;
        var valor = columna == "nombre_producto"
            ? producto.Nombre
            : producto.Id.ToString();

        var query = AplicarFiltros(
            client.From<Modelados.Productos.Productos>()
                  .Select("id_producto")
                  .Filter(columna, comparador, valor),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static Table AplicarFiltros(Table query, ProductoFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
        if (filtros.IdFabricante.HasValue)
            query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());
        if (filtros.IdCategoria.HasValue)
            query = query.Filter("id_categoria",  Op.Equals, filtros.IdCategoria.Value.ToString());

        // Filtro sobre el recurso embebido; requiere el !inner de SelectPara.
        if (filtros.IdProveedor.HasValue)
            query = query.Filter("fabricante.id_proveedor", Op.Equals, filtros.IdProveedor.Value.ToString());

        return query;
    }

    /// <summary>
    /// C12: Obtiene conteos via RPC contar_productos — un viaje al servidor,
    /// sin descargar filas. Reemplaza el workaround de GET + .Models.Count.
    /// </summary>
    private static async Task<(int total, int activos, int inactivos)> GetConteosRpcAsync(
        ProductoFiltros filtros, Supabase.Client client)
    {
        var parametros = new Dictionary<string, object?>();
        if (filtros.IdEstado.HasValue)     parametros["p_estado"] = filtros.IdEstado.Value;
        if (filtros.IdFabricante.HasValue) parametros["p_fab"]    = filtros.IdFabricante.Value;
        if (filtros.IdPais.HasValue)       parametros["p_pais"]   = filtros.IdPais.Value;
        if (filtros.IdProveedor.HasValue)  parametros["p_prov"]   = filtros.IdProveedor.Value;
        if (filtros.IdCategoria.HasValue)  parametros["p_cat"]    = filtros.IdCategoria.Value;

        var response = await client.Rpc("contar_productos", parametros);
        var json     = response?.Content;

        if (string.IsNullOrWhiteSpace(json))
            return (0, 0, 0);

        var arr  = JArray.Parse(json);
        var row  = arr.FirstOrDefault() as JObject;
        if (row is null) return (0, 0, 0);

        int total   = row["total"]?.Value<int>()    ?? 0;
        int activos = row["activos"]?.Value<int>()  ?? 0;
        int inact   = row["inactivos"]?.Value<int>() ?? 0;
        return (total, activos, inact);
    }
}
