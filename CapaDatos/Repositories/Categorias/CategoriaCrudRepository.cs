using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Categorias.Queries;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaDatos.Modelados.Productos;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Productos.Categoria>;

namespace CapaDatos.Repositories.Categorias;

public class CategoriaCrudRepository : RepositorioBase, ICategoriaRepository
{
    private static CategoriaDto Map(Categoria c) => new()
    {
        Id              = c.idCategoria,
        Nombre          = c.nombreCategoria     ?? string.Empty,
        Descripcion     = c.descripcionCategoria ?? string.Empty,
        EstadoCategoria = c.estadoCategoria,
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<CategoriaDto>>> GetPagedAsync(
        int page, int size, CategoriaFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar categorías");

    public Task<Result<IReadOnlyList<CategoriaDto>>> BuscarSugerenciasAsync(
        string termino, CategoriaFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias categorías");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, CategoriaFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de categoría");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(CategoriaDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nueva  = new Categoria
            {
                nombreCategoria     = dto.Nombre,
                descripcionCategoria = dto.Descripcion,
                estadoCategoria     = dto.EstadoCategoria,
            };
            var resultado = await client.From<Categoria>().Insert(nueva);
            return resultado.Models.First().idCategoria;
        }, "Crear categoría");

    public Task<Result> UpdateAsync(CategoriaDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Categoria>()
                .Where(c => c.idCategoria == dto.Id)
                .Set(c => c.nombreCategoria,      dto.Nombre)
                .Set(c => c.descripcionCategoria, dto.Descripcion)
                .Set(c => c.estadoCategoria,      dto.EstadoCategoria)
                .Update();
        }, "Actualizar categoría");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Categoria>()
                .Where(c => c.idCategoria == id)
                .Set(c => c.estadoCategoria, false)
                .Update();
        }, "Eliminar categoría");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<CategoriaDto>> GetPagedInternal(
        int page, int size, CategoriaFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Categoria>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("id_categoria", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtros, client);
        await Task.WhenAll(pageTask, conteosTask);

        var items   = pageTask.Result?.Models.Select(Map).ToList() ?? [];
        var conteos = conteosTask.Result;

        return new PagedResult<CategoriaDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<CategoriaDto>> BuscarSugerenciasInternal(
        string termino, CategoriaFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Categoria>().Select("*"), filtros);

        var resultado = await query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_categoria",     Op.ILike, $"%{termino}%"),
                new QueryFilter("descripcion_categoria", Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_categoria", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, CategoriaFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<Categoria>()
                  .Select("id_categoria")
                  .Filter("id_categoria", Op.LessThan, id.ToString()),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    /// <summary>
    /// Traduce IdEstado (int) a estado_categoria (bool) para el filtro de Supabase.
    /// 1 (Activo) → estado_categoria = true
    /// 2 (Inactivo) → estado_categoria = false
    /// null → sin filtro
    /// </summary>
    private static Table AplicarFiltros(Table query, CategoriaFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
        {
            bool activo = filtros.IdEstado.Value == EstadoRegistro.Activo;
            query = query.Filter("estado_categoria", Op.Equals, activo.ToString().ToLower());
        }
        return query;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosRpcAsync(
        CategoriaFiltros filtros, Supabase.Client client)
    {
        var parametros = new Dictionary<string, object?>();
        if (filtros.IdEstado.HasValue)
            parametros["p_estado"] = filtros.IdEstado.Value == EstadoRegistro.Activo;

        var response = await client.Rpc("contar_categorias", parametros);
        var json     = response?.Content;
        if (string.IsNullOrWhiteSpace(json)) return (0, 0, 0);

        var arr = JArray.Parse(json);
        var row = arr.FirstOrDefault() as JObject;
        if (row is null) return (0, 0, 0);

        return (row["total"]?.Value<int>() ?? 0,
                row["activos"]?.Value<int>() ?? 0,
                row["inactivos"]?.Value<int>() ?? 0);
    }
}
