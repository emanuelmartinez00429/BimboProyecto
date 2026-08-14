using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
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
    public ProductoCrudRepository(IConexionMonitor conexion) : base(conexion) { }

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

        return $"*, presentacion_producto(*), {fabricante}, categoria(*), paises(*), tara(*, unidad_medida(*)), unidad_medida(*)";
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
        Contenido      = p.contenidoProducto ?? string.Empty,
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
        IdTara         = p.idTara,
        Tara           = p.descripcion_Tara,
        IdUnidad       = p.idUnidad,
        Unidad         = p.abreviatura_Unidad,
        PrecioPorKg    = p.precioPorKg,
        CreatedAt      = p.createdAt,
        UpdatedAt      = p.updatedAt,
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
        ProductoDto producto, int size, ProductoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeProductoInternal(producto, size, filtros), "Calcular página de producto");

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
                pesoTeorico       = dto.PesoTeorico,
                idTara            = dto.IdTara,
                idUnidad          = dto.IdUnidad,
                precioPorKg       = dto.PrecioPorKg,
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
                .Set(p => p.pesoTeorico,       dto.PesoTeorico)
                .Set(p => p.idTara,            dto.IdTara)
                .Set(p => p.idUnidad,          dto.IdUnidad)
                .Set(p => p.precioPorKg,       dto.PrecioPorKg)
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
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(SelectPara(filtros)), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        // Los conteos van SIN el filtro de estado: las pastillas TOTAL/ACTIVOS/
        // INACTIVOS desglosan justamente por estado, así que si se les pasa
        // p_estado terminan todas acotadas al mismo subconjunto (p.ej. filtrando
        // "Activos", TOTAL deja de ser el total real y pasa a valer lo mismo que
        // ACTIVOS). Fabricante/país sí se respetan porque son ortogonales al estado.
        var filtrosConteo = new ProductoFiltros
        {
            IdFabricante = filtros.IdFabricante,
            IdPais       = filtros.IdPais,
            IdProveedor  = filtros.IdProveedor,
        };

        var (colOrden, dirOrden) = ColumnaOrden(filtros.Orden);

        // Página + conteos en paralelo (conteos via RPC — sin descargar filas)
        var pageTask    = query.Order(colOrden, dirOrden).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtrosConteo, client);
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
        var query  = AplicarFiltros(client.From<Modelados.Productos.Productos>().Select(SelectPara(filtros)), filtros);

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
