using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Categorias.Queries;
using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Productos;
using Newtonsoft.Json;
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
    private readonly IUsuarioSesionService _sesionService;

    public CategoriaCrudRepository(IConexionMonitor conexion, IUsuarioSesionService sesionService)
        : base(conexion) => _sesionService = sesionService;

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

    public Task<Result<int>> CreateAsync(CategoriaDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_categoria"] = dto.Nombre,
                ["p_descripcion_categoria"] = dto.Descripcion,
                ["p_id_solicitud"] = idSolicitud,
            };

            var response = await client.Rpc("crear_categoria_seguro", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "categoría", "id_categoria");
        }, "Crear categoría");

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

    public Task<Result> UpdateAsync(CategoriaDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_categoria"] = dto.Id,
                ["p_nombre_categoria"] = dto.Nombre,
                ["p_descripcion_categoria"] = dto.Descripcion,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("actualizar_categoria_seguro", parametros);
        }, "Actualizar categoría");

    public Task<Result> DeleteAsync(int id, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_categoria"] = id,
                ["p_estado_categoria"] = false,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("cambiar_estado_categoria_seguro", parametros);
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
