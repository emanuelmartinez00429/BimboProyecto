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

    public Task<Result<int>> CreateAsync(FabricanteDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No hay una sesión activa; no se puede crear el fabricante.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_fabricante"] = dto.Nombre,
                ["p_descripcion_fabricante"] = dto.Descripcion,
                ["p_id_proveedor"] = dto.IdProveedor,
                ["p_id_estado"] = dto.IdEstado,
                ["p_id_pais"] = dto.IdPais,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc("ingresar_fabricante_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "fabricante");
        }, "Crear fabricante");

    private static int ObtenerIdCreado(string? json, string entidad)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"La función de creación no devolvió el identificador del {entidad}.");
        int id;
        try { id = JToken.Parse(json).ToObject<int>(); }
        catch (Exception ex) when (ex is JsonException or FormatException)
        { throw new InvalidOperationException("La función de creación devolvió un identificador inválido.", ex); }
        return id > 0 ? id : throw new InvalidOperationException("La función de creación devolvió un identificador inválido.");
    }

    public Task<Result> UpdateAsync(FabricanteDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<FabricanteCrud>()
                .Where(f => f.idFabricante == dto.Id)
                .Set(f => f.nombreFabricante,      dto.Nombre)
                .Set(f => f.descripcionFabricante, dto.Descripcion)
                .Set(f => f.idProveedor,           dto.IdProveedor)
                .Set(f => f.idPais,                dto.IdPais)
                .Set(f => f.idEstado,              dto.IdEstado)
                .Update();
        }, "Actualizar fabricante");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<FabricanteCrud>()
                .Where(f => f.idFabricante == id)
                .Set(f => f.idEstado, EstadoRegistro.Inactivo)
                .Update();
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
