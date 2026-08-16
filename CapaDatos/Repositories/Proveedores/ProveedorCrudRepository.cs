using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Queries;
using CapaAplicacion.Usuarios.Interfaces;
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

    public ProveedorCrudRepository(IConexionMonitor conexion, IUsuarioSesionService sesionService)
        : base(conexion) => _sesionService = sesionService;

    private static ProveedorDto Map(Prov p) => new()
    {
        Id        = p.idProveedor,
        Nombre    = p.nombreProveedor    ?? string.Empty,
        Rtn       = p.rtnProveedor       ?? string.Empty,
        Telefono  = p.telefonoProveedor  ?? string.Empty,
        Correo    = p.correoProveedor    ?? string.Empty,
        Direccion = p.direccionProveedor ?? string.Empty,
        IdEstado  = p.idEstado,
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<ProveedorDto>>> GetPagedAsync(
        int page, int size, ProveedorFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar proveedores");

    public Task<Result<IReadOnlyList<ProveedorDto>>> BuscarSugerenciasAsync(
        string termino, ProveedorFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias proveedores");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, ProveedorFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de proveedor");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(ProveedorDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No hay una sesión activa; no se puede crear el proveedor.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_proveedor"] = dto.Nombre,
                ["p_rtn_proveedor"] = dto.Rtn,
                ["p_telefono_proveedor"] = dto.Telefono,
                ["p_correo_proveedor"] = dto.Correo,
                ["p_direccion_proveedor"] = dto.Direccion,
                ["p_id_estado"] = dto.IdEstado,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc("ingresar_proveedor_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "proveedor");
        }, "Crear proveedor");

    private static int ObtenerIdCreado(string? json, string entidad)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"La función de creación no devolvió el identificador del {entidad}.");

        int id;
        try { id = JToken.Parse(json).ToObject<int>(); }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidOperationException("La función de creación devolvió un identificador inválido.", ex);
        }

        return id > 0
            ? id
            : throw new InvalidOperationException("La función de creación devolvió un identificador inválido.");
    }

    public Task<Result> UpdateAsync(ProveedorDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Prov>()
                .Where(p => p.idProveedor == dto.Id)
                .Set(p => p.nombreProveedor,    dto.Nombre)
                .Set(p => p.rtnProveedor,       dto.Rtn)
                .Set(p => p.telefonoProveedor,  dto.Telefono)
                .Set(p => p.correoProveedor,    dto.Correo)
                .Set(p => p.direccionProveedor, dto.Direccion)
                .Set(p => p.idEstado,           dto.IdEstado)
                .Update();
        }, "Actualizar proveedor");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Prov>()
                .Where(p => p.idProveedor == id)
                .Set(p => p.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar proveedor");

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

        var resultado = await query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_proveedor", Op.ILike, $"%{termino}%"),
                new QueryFilter("rtn_proveedor",    Op.ILike, $"%{termino}%"),
            })
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
