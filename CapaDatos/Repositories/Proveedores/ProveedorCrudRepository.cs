using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Cache;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Prov  = CapaDatos.Modelados.Pesajes.Proveedores;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Pesajes.Proveedores>;

namespace CapaDatos.Repositories.Proveedores;

public class ProveedorCrudRepository : RepositorioBase, IProveedorRepository
{
    private readonly IUsuarioSesionService _sesionService;
    private readonly ICacheService         _cache;

    public ProveedorCrudRepository(
        IConexionMonitor conexion,
        IUsuarioSesionService sesionService,
        ICacheService cache) : base(conexion)
    {
        _sesionService = sesionService;
        _cache         = cache;
    }

    private static ProveedorDto Map(Prov p) => new()
    {
        Id        = p.idProveedor,
        Nombre    = p.nombreProveedor    ?? string.Empty,
        Rtn       = p.rtnProveedor       ?? string.Empty,
        Telefono  = p.telefonoProveedor  ?? string.Empty,
        Correo    = p.correoProveedor    ?? string.Empty,
        Direccion = p.direccionProveedor ?? string.Empty,
        IdEstado  = p.idEstado,
        CreatedAt = p.createdAt?.ToLocalTime(),
        UpdatedAt = p.updatedAt?.ToLocalTime(),
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<ProveedorDto>>> GetPagedAsync(
        int page, int size, ProveedorFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar proveedores");

    public Task<Result<IReadOnlyList<ProveedorDto>>> BuscarSugerenciasAsync(
        string termino, ProveedorFiltros filtros, CancellationToken ct = default)
    {
        var aguja = TextoBusqueda.Normalizar(termino).Trim();
        if (aguja.Length < 3)
            return TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias proveedores");

        var estado = filtros.IdEstado?.ToString() ?? "todos";
        var clave  = $"sug:{TagsCache.TablaProveedores}:{aguja}:{estado}";

        return _cache.ObtenerOCrearAsync(
            clave,
            _ => TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias proveedores"),
            PoliticasCache.Sugerencias,
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaProveedores),
            ct: ct);
    }

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, ProveedorFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de proveedor");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(ProveedorDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_proveedor"] = dto.Nombre,
                ["p_rtn_proveedor"] = dto.Rtn,
                ["p_telefono_proveedor"] = dto.Telefono,
                ["p_correo_proveedor"] = dto.Correo,
                ["p_direccion_proveedor"] = dto.Direccion,
                ["p_id_solicitud"] = idSolicitud,
            };

            var response = await client.Rpc("crear_proveedor_seguro", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "proveedor", "id_proveedor");
        }, "Crear proveedor");

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

    public Task<Result> UpdateAsync(ProveedorDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_proveedor"] = dto.Id,
                ["p_nombre_proveedor"] = dto.Nombre,
                ["p_rtn_proveedor"] = dto.Rtn,
                ["p_telefono_proveedor"] = dto.Telefono,
                ["p_correo_proveedor"] = dto.Correo,
                ["p_direccion_proveedor"] = dto.Direccion,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("actualizar_proveedor_seguro", parametros);
        }, "Actualizar proveedor");

    public Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_proveedor"] = id,
                ["p_id_estado"] = nuevoEstado,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("cambiar_estado_proveedor_seguro", parametros);
        }, "Cambiar estado de proveedor");

    public Task<Result> DeleteAsync(int id, Guid idSolicitud, CancellationToken ct = default) =>
        CambiarEstadoAsync(id, EstadoRegistro.Inactivo, idSolicitud, ct);

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<ProveedorDto>> GetPagedInternal(
        int page, int size, ProveedorFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Prov>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("id_proveedor", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtros, client);
        await Task.WhenAll(pageTask, conteosTask);

        var items   = pageTask.Result?.Models.Select(Map).ToList() ?? [];
        var conteos = conteosTask.Result;

        return new PagedResult<ProveedorDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<ProveedorDto>> BuscarSugerenciasInternal(
        string termino, ProveedorFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Prov>().Select("*"), filtros);

        var aguja = TextoBusqueda.Normalizar(termino);
        var resultado = await query
            .Filter("busqueda_proveedor", Op.ILike, $"%{aguja}%")
            .Order("nombre_proveedor", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, ProveedorFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<Prov>()
                  .Select("id_proveedor")
                  .Filter("id_proveedor", Op.LessThan, id.ToString()),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static Table AplicarFiltros(Table query, ProveedorFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
        return query;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosRpcAsync(
        ProveedorFiltros filtros, Supabase.Client client)
    {
        var parametros = new Dictionary<string, object?>();
        if (filtros.IdEstado.HasValue) parametros["p_estado"] = filtros.IdEstado.Value;

        var response = await client.Rpc("contar_proveedores", parametros);
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
