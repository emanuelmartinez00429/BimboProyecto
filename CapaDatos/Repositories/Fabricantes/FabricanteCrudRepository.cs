using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Queries;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Fabricantes;
using CapaDatos.Modelados.Productos;
using ProveedorEnt = CapaDatos.Modelados.Pesajes.Proveedores;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Fabricantes.FabricanteCrud>;

namespace CapaDatos.Repositories.Fabricantes;

public class FabricanteCrudRepository : RepositorioBase, IFabricanteRepository
{
    private readonly IUsuarioSesionService _sesionService;

    public FabricanteCrudRepository(IConexionMonitor conexion, IUsuarioSesionService sesionService)
        : base(conexion) => _sesionService = sesionService;

    private static FabricanteDto Map(
        FabricanteCrud f,
        Dictionary<int, string> provDic,
        Dictionary<int, string> paisDic) => new()
    {
        Id              = f.idFabricante,
        Nombre          = f.nombreFabricante     ?? string.Empty,
        Descripcion     = f.descripcionFabricante ?? string.Empty,
        IdProveedor     = f.idProveedor,
        NombreProveedor = f.idProveedor.HasValue ? provDic.GetValueOrDefault(f.idProveedor.Value, "") : "",
        IdPais          = f.idPais,
        NombrePais      = f.idPais.HasValue      ? paisDic.GetValueOrDefault(f.idPais.Value,      "") : "",
        IdEstado        = f.idEstado,
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<FabricanteDto>>> GetPagedAsync(
        int page, int size, FabricanteFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar fabricantes");

    public Task<Result<IReadOnlyList<FabricanteDto>>> BuscarSugerenciasAsync(
        string termino, FabricanteFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias fabricantes");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetPaisesAsync(CancellationToken ct = default) =>
        TryAsync(GetPaisesInternal, "Cargar países");

    public Task<Result<IReadOnlyList<FiltroItem>>> GetProveedoresAsync(CancellationToken ct = default) =>
        TryAsync(GetProveedoresInternal, "Cargar proveedores");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, FabricanteFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de fabricante");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(FabricanteDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_fabricante"] = dto.Nombre,
                ["p_descripcion_fabricante"] = dto.Descripcion,
                ["p_id_proveedor"] = dto.IdProveedor,
                ["p_id_pais"] = dto.IdPais,
                ["p_id_solicitud"] = idSolicitud,
            };

            var response = await client.Rpc("crear_fabricante_seguro", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "fabricante", "id_fabricante");
        }, "Crear fabricante");

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

    public Task<Result> UpdateAsync(FabricanteDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_fabricante"] = dto.Id,
                ["p_nombre_fabricante"] = dto.Nombre,
                ["p_descripcion_fabricante"] = dto.Descripcion,
                ["p_id_proveedor"] = dto.IdProveedor,
                ["p_id_pais"] = dto.IdPais,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("actualizar_fabricante_seguro", parametros);
        }, "Actualizar fabricante");

    public Task<Result> DeleteAsync(int id, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_fabricante"] = id,
                ["p_id_estado"] = EstadoRegistro.Inactivo,
                ["p_id_solicitud"] = idSolicitud,
            };
            await client.Rpc("cambiar_estado_fabricante_seguro", parametros);
        }, "Eliminar fabricante");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<FabricanteDto>> GetPagedInternal(
        int page, int size, FabricanteFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<FabricanteCrud>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("id_fabricante", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtros, client);
        var provDicTask = GetProveedoresDicAsync(client);
        var paisDicTask = GetPaisesDicAsync(client);
        await Task.WhenAll(pageTask, conteosTask, provDicTask, paisDicTask);

        var provDic = provDicTask.Result;
        var paisDic = paisDicTask.Result;
        var items   = pageTask.Result?.Models.Select(f => Map(f, provDic, paisDic)).ToList() ?? [];
        var conteos = conteosTask.Result;

        return new PagedResult<FabricanteDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<FabricanteDto>> BuscarSugerenciasInternal(
        string termino, FabricanteFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<FabricanteCrud>().Select("*"), filtros);

        var resultTask  = query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_fabricante", Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_fabricante", Ord.Ascending)
            .Limit(10)
            .Get();
        var provDicTask = GetProveedoresDicAsync(client);
        var paisDicTask = GetPaisesDicAsync(client);
        await Task.WhenAll(resultTask, provDicTask, paisDicTask);

        var provDic = provDicTask.Result;
        var paisDic = paisDicTask.Result;
        return resultTask.Result?.Models.Select(f => Map(f, provDic, paisDic)).ToList() ?? [];
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

    private async Task<IReadOnlyList<FiltroItem>> GetProveedoresInternal()
    {
        var client    = await ConexionSupabase.GetClientAsync();
        var resultado = await client.From<ProveedorEnt>()
            .Select("id_proveedor, nombre_proveedor")
            .Filter("id_estado", Op.Equals, EstadoRegistro.Activo.ToString())
            .Order("nombre_proveedor", Ord.Ascending)
            .Get();

        return (resultado?.Models ?? [])
            .Select(p => new FiltroItem { Id = p.idProveedor, Nombre = p.nombreProveedor })
            .ToList();
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, FabricanteFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<FabricanteCrud>()
                  .Select("id_fabricante")
                  .Filter("id_fabricante", Op.LessThan, id.ToString()),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static async Task<Dictionary<int, string>> GetProveedoresDicAsync(Supabase.Client client)
    {
        var r = await client.From<ProveedorEnt>()
            .Select("id_proveedor, nombre_proveedor")
            .Get();
        return (r?.Models ?? [])
            .Where(p => p.idProveedor > 0)
            .ToDictionary(p => p.idProveedor, p => p.nombreProveedor ?? "");
    }

    private static async Task<Dictionary<int, string>> GetPaisesDicAsync(Supabase.Client client)
    {
        var r = await client.From<Paises>()
            .Select("id_pais, nombre_pais")
            .Get();
        return (r?.Models ?? [])
            .Where(p => p.idPais > 0)
            .ToDictionary(p => p.idPais, p => p.nombrePais ?? "");
    }

    private static Table AplicarFiltros(Table query, FabricanteFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
        if (filtros.IdPais.HasValue)
            query = query.Filter("id_pais", Op.Equals, filtros.IdPais.Value.ToString());
        return query;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosRpcAsync(
        FabricanteFiltros filtros, Supabase.Client client)
    {
        var parametros = new Dictionary<string, object?>();
        if (filtros.IdEstado.HasValue) parametros["p_estado"] = filtros.IdEstado.Value;
        if (filtros.IdPais.HasValue)   parametros["p_pais"]   = filtros.IdPais.Value;

        var response = await client.Rpc("contar_fabricantes", parametros);
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
