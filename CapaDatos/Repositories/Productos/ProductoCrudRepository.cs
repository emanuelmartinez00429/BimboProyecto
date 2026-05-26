using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Ct  = Supabase.Postgrest.Constants.CountType;

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
                .Set(p => p.idEstado, 2)
                .Update();
        }, "Eliminar producto");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<ProductoDto>> GetPagedInternal(
        int page, int size, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = client.From<Modelados.Productos.Productos>().Select(Select);

        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
        if (filtros.IdFabricante.HasValue)
            query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());

        int from = (page - 1) * size;
        int to   = from + size - 1;

        // Página + conteos en paralelo
        var pageTask    = query.Order("id_producto", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosAsync(filtros, client);
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
        var query  = client.From<Modelados.Productos.Productos>().Select(Select);

        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
        if (filtros.IdFabricante.HasValue)
            query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());

        var resultado = await query
            .Filter("nombre_producto", Op.ILike, $"%{termino}%")
            .Order("nombre_producto",  Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<IReadOnlyList<FiltroItem>> GetFabricantesInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<Modelados.Productos.Productos>()
            .Select("id_fabricante, fabricante(nombre_fabricante)")
            .Get();

        return (resultado?.Models ?? [])
            .Where(p => p.Fabricante != null)
            .Select(p => (p.idFabricante, p.Fabricante!.nombreFabricante))
            .DistinctBy(x => x.idFabricante)
            .OrderBy(x => x.nombreFabricante)
            .Select(x => new FiltroItem { Id = x.idFabricante, Nombre = x.nombreFabricante })
            .ToList();
    }

    private async Task<IReadOnlyList<FiltroItem>> GetPaisesInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<Modelados.Productos.Productos>()
            .Select("id_pais, paises(nombre_pais)")
            .Get();

        return (resultado?.Models ?? [])
            .Where(p => p.Paises != null)
            .Select(p => (p.idPais, p.Paises!.nombrePais))
            .DistinctBy(x => x.idPais)
            .OrderBy(x => x.nombrePais)
            .Select(x => new FiltroItem { Id = x.idPais, Nombre = x.nombrePais })
            .ToList();
    }

    private async Task<IReadOnlyList<FiltroItem>> GetCategoriasInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<Modelados.Productos.Productos>()
            .Select("id_categoria, categoria(nombre_categoria)")
            .Get();

        return (resultado?.Models ?? [])
            .Where(p => p.Categoria != null)
            .Select(p => (p.idCategoria, p.Categoria!.nombreCategoria))
            .DistinctBy(x => x.idCategoria)
            .OrderBy(x => x.nombreCategoria)
            .Select(x => new FiltroItem { Id = x.idCategoria, Nombre = x.nombreCategoria })
            .ToList();
    }

    private async Task<int> GetPaginaDeProductoInternal(int idProducto, int size, ProductoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = client.From<Modelados.Productos.Productos>()
                           .Select("id_producto")
                           .Filter("id_producto", Op.LessThan, idProducto.ToString());

        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado",     Op.Equals, filtros.IdEstado.Value.ToString());
        if (filtros.IdFabricante.HasValue)
            query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            query = query.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosAsync(
        ProductoFiltros filtros, Supabase.Client client)
    {
        // Count() no aplica filtros correctamente en esta versión del cliente.
        // Se construyen dos queries independientes y se usa Get() que sí respeta los filtros.
        var qTotal = client.From<Modelados.Productos.Productos>().Select("id_producto");
        if (filtros.IdFabricante.HasValue)
            qTotal = qTotal.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            qTotal = qTotal.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());

        var qActivos = client.From<Modelados.Productos.Productos>().Select("id_producto")
                             .Filter("id_estado", Op.Equals, "1");
        if (filtros.IdFabricante.HasValue)
            qActivos = qActivos.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
        if (filtros.IdPais.HasValue)
            qActivos = qActivos.Filter("id_pais",       Op.Equals, filtros.IdPais.Value.ToString());

        var totalTask   = qTotal.Get();
        var activosTask = qActivos.Get();
        await Task.WhenAll(totalTask, activosTask);

        int total   = totalTask.Result?.Models.Count   ?? 0;
        int activos = activosTask.Result?.Models.Count ?? 0;
        return (total, activos, total - activos);
    }
}
