using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Presentaciones.Queries;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Cache;
using CapaDatos.Modelados.Productos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Productos.PresentacionCrud>;

namespace CapaDatos.Repositories.Presentaciones;

public class PresentacionCrudRepository : RepositorioBase, IPresentacionRepository
{
    private readonly IUsuarioSesionService _sesionService;
    private readonly ICacheService         _cache;

    public PresentacionCrudRepository(
        IConexionMonitor conexion,
        IUsuarioSesionService sesionService,
        ICacheService cache) : base(conexion)
    {
        _sesionService = sesionService;
        _cache         = cache;
    }

    private static PresentacionDto Map(PresentacionCrud p) => new()
    {
        Id          = p.idPresentacion,
        Nombre      = p.nombrePresentacion      ?? string.Empty,
        Descripcion = p.descripcionPresentacion ?? string.Empty,
        IdEstado    = p.idEstado,
        CreatedAt   = p.createdAt?.ToLocalTime(),
        UpdatedAt   = p.updatedAt?.ToLocalTime(),
    };

    /// <summary>
    /// Fuente única del orden: la usan el listado y el cálculo de página del
    /// buscador. Si se desincronizan, seleccionar una sugerencia salta a la
    /// página equivocada.
    /// </summary>
    private static (string columna, Ord direccion) ColumnaOrden(OrdenPresentacion orden) => orden switch
    {
        OrdenPresentacion.NombreAsc  => ("nombre_presentacion", Ord.Ascending),
        OrdenPresentacion.NombreDesc => ("nombre_presentacion", Ord.Descending),
        _                            => ("id_presentacion",     Ord.Ascending),
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<PresentacionDto>>> GetPagedAsync(
        int page, int size, PresentacionFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar presentaciones");

    public Task<Result<IReadOnlyList<PresentacionDto>>> BuscarSugerenciasAsync(
        string termino, PresentacionFiltros filtros, CancellationToken ct = default)
    {
        var aguja = TextoBusqueda.Normalizar(termino).Trim();
        if (aguja.Length < 3)
            return TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias presentaciones");

        var estado = filtros.IdEstado?.ToString() ?? "todos";
        var clave  = $"sug:{TagsCache.TablaPresentacion}:{aguja}:{estado}";

        return _cache.ObtenerOCrearAsync(
            clave,
            _ => TryAsync(() => BuscarSugerenciasInternal(aguja, filtros), "Buscar sugerencias presentaciones"),
            PoliticasCache.Sugerencias,
            etiquetas: TagsCache.DeCatalogo(TagsCache.TablaPresentacion),
            ct: ct);
    }

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        PresentacionDto dto, int size, PresentacionFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(dto, size, filtros), "Calcular página de presentación");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(PresentacionDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No hay una sesión activa; no se puede crear la presentación.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_nombre_presentacion"] = dto.Nombre,
                ["p_descripcion_presentacion"] = dto.Descripcion,
                ["p_id_estado"] = dto.IdEstado,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc("ingresar_presentacion_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content, "presentación");
        }, "Crear presentación");

    private static int ObtenerIdCreado(string? json, string entidad)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"La función de creación no devolvió el identificador de la {entidad}.");
        int id;
        try { id = JToken.Parse(json).ToObject<int>(); }
        catch (Exception ex) when (ex is JsonException or FormatException)
        { throw new InvalidOperationException("La función de creación devolvió un identificador inválido.", ex); }
        return id > 0 ? id : throw new InvalidOperationException("La función de creación devolvió un identificador inválido.");
    }

    public Task<Result> UpdateAsync(PresentacionDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            // updated_at no se toca: lo mueve el trigger trg_presentacion_updated_at.
            await client.From<PresentacionCrud>()
                .Where(p => p.idPresentacion == dto.Id)
                .Set(p => p.nombrePresentacion,      dto.Nombre)
                .Set(p => p.descripcionPresentacion!, dto.Descripcion)
                .Set(p => p.idEstado,                dto.IdEstado)
                .Update();
        }, "Actualizar presentación");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<PresentacionCrud>()
                .Where(p => p.idPresentacion == id)
                .Set(p => p.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar presentación");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<PresentacionDto>> GetPagedInternal(
        int page, int size, PresentacionFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<PresentacionCrud>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var (colOrden, dirOrden) = ColumnaOrden(filtros.Orden);

        // Los conteos van SIN el filtro de estado: las pastillas TOTAL/ACTIVOS/
        // INACTIVOS desglosan justamente por estado, así que pasarle p_estado al
        // RPC las dejaría a las tres acotadas al mismo subconjunto (filtrando
        // "Inactivos" daría ACTIVOS = 0 y TOTAL = INACTIVOS). Mismo criterio que
        // ProductoCrudRepository. Presentaciones no tiene otros filtros que
        // sean ortogonales al estado, así que el RPC va sin parámetros.
        var pageTask    = query.Order(colOrden, dirOrden).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(client);
        await Task.WhenAll(pageTask, conteosTask);

        var items   = pageTask.Result?.Models.Select(Map).ToList() ?? [];
        var conteos = conteosTask.Result;

        return new PagedResult<PresentacionDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<PresentacionDto>> BuscarSugerenciasInternal(
        string termino, PresentacionFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<PresentacionCrud>().Select("*"), filtros);

        var (colOrden, dirOrden) = ColumnaOrden(filtros.Orden);

        var resultado = await query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_presentacion",      Op.ILike, $"%{termino}%"),
                new QueryFilter("descripcion_presentacion", Op.ILike, $"%{termino}%"),
            })
            .Order(colOrden, dirOrden)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(
        PresentacionDto dto, int size, PresentacionFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();

        var (columna, direccion) = ColumnaOrden(filtros.Orden);

        // "Antes que" se invierte con el orden descendente.
        var comparador = direccion == Ord.Ascending ? Op.LessThan : Op.GreaterThan;
        var valor      = columna == "nombre_presentacion" ? dto.Nombre : dto.Id.ToString();

        var query = AplicarFiltros(
            client.From<PresentacionCrud>()
                  .Select("id_presentacion")
                  .Filter(columna, comparador, valor),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static Table AplicarFiltros(Table query, PresentacionFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
        return query;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosRpcAsync(
        Supabase.Client client)
    {
        var response = await client.Rpc("contar_presentaciones", new Dictionary<string, object?>());
        var json     = response?.Content;
        if (string.IsNullOrWhiteSpace(json)) return (0, 0, 0);

        var arr = JArray.Parse(json);
        var row = arr.FirstOrDefault() as JObject;
        if (row is null) return (0, 0, 0);

        return (row["total"]?.Value<int>()     ?? 0,
                row["activos"]?.Value<int>()   ?? 0,
                row["inactivos"]?.Value<int>() ?? 0);
    }
}
