using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
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
    private const string Select =
        "*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)";

    private static ProductoDto Map(Modelados.Productos.Productos p) => new()
    {
        Id             = p.idProducto,
        CodigoInterno  = p.codigoProducto    ?? string.Empty,
        Nombre         = p.nombreProducto    ?? string.Empty,
        Contenido      = p.contenidoProducto ?? string.Empty,
        Presentacion   = p.nombre_Presentacion,
        Fabricante     = p.nombre_Fabricante,
        Categoria      = p.nombre_Categoria,
        Pais           = p.nombre_Pais,
        IdEstado       = p.idEstado,
        IdFabricante   = p.idFabricante,
        IdCategoria    = p.idCategoria,
        IdPais         = p.idPais,
        IdPresentacion = p.idPresentacion,
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(
        int page, int size, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar productos");

    public Task<Result<IReadOnlyList<ProductoDto>>> BuscarSugerenciasAsync(
        string termino, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetFabricantesAsync(CancellationToken ct = default) =>
        TryAsync(GetFabricantesInternal, "Cargar fabricantes");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetPaisesAsync(CancellationToken ct = default) =>
        TryAsync(GetPaisesInternal, "Cargar países");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetCategoriasAsync(CancellationToken ct = default) =>
        TryAsync(GetCategoriasInternal, "Cargar categorías");

    public Task<Result<int>> GetPaginaDeProductoAsync(
        int idProducto, int size, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeProductoInternal(idProducto, size, filtros), "Calcular página de producto");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(ProductoDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nuevo  = new Modelados.Productos.Productos
            {
                codigoProducto    = dto.CodigoInterno,
                nombreProducto    = dto.Nombre,
                contenidoProducto = dto.Contenido,
                idPresentacion    = dto.IdPresentacion,
                idFabricante      = dto.IdFabricante,
                idCategoria       = dto.IdCategoria,
                idPais            = dto.IdPais,
                idEstado          = dto.IdEstado,
            };
            var resultado = await client.From<Modelados.Productos.Productos>().Insert(nuevo);
            return resultado.Models.First().idProducto;
        }, "Crear producto");

    public Task<Result> UpdateAsync(ProductoDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Modelados.Productos.Productos>()
                .Where(p => p.idProducto == dto.Id)
                .Set(p => p.codigoProducto,    dto.CodigoInterno)
                .Set(p => p.nombreProducto,    dto.Nombre)
                .Set(p => p.contenidoProducto, dto.Contenido)
                .Set(p => p.idPresentacion,    dto.IdPresentacion)
                .Set(p => p.idFabricante,      dto.IdFabricante)
                .Set(p => p.idCategoria,       dto.IdCategoria)
                .Set(p => p.idPais,            dto.IdPais)
                .Set(p => p.idEstado,          dto.IdEstado)
                .Update();
        }, "Actualizar producto");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Modelados.Productos.Productos>()
                .Where(p => p.idProducto == id)
                .Set(p => p.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar producto");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<ProductoDto>> GetPagedInternal(
        int page, int size, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(Select), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        // Página + conteos en paralelo (conteos via RPC — sin descargar filas)
        var pageTask    = query.Order("id_producto", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtros, client);
        await Task.WhenAll(pageTask, conteosTask);

        var items   = pageTask.Result?.Models.Select(Map).ToList() ?? [];
        var conteos = conteosTask.Result;

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
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(Select), filtros);

        var resultado = await query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_producto", Op.ILike, $"%{termino}%"),
                new QueryFilter("codigo_producto",  Op.ILike, $"%{termino}%"),
            })
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

    private async Task<int> GetPaginaDeProductoInternal(int idProducto, int size, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<Modelados.Productos.Productos>()
                  .Select("id_producto")
                  .Filter("id_producto", Op.LessThan, idProducto.ToString()),
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
