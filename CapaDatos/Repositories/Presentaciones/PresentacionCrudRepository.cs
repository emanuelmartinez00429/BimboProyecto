using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Presentaciones.Queries;
using CapaAplicacion.Productos.Queries;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;
using Pres  = CapaDatos.Modelados.Productos.Presentacion;
using Table = Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Productos.Presentacion>;

namespace CapaDatos.Repositories.Presentaciones;

public class PresentacionCrudRepository : RepositorioBase, IPresentacionRepository
{
    public PresentacionCrudRepository(IConexionMonitor conexion) : base(conexion) { }

    private static PresentacionDto Map(Pres p) => new()
    {
        Id          = p.idPresentacion,
        Nombre      = p.nombrePresentacion      ?? string.Empty,
        Descripcion = p.descripcionPresentacion ?? string.Empty,
        IdEstado    = p.idEstado,
    };

    // ── Lectura ───────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<PresentacionDto>>> GetPagedAsync(
        int page, int size, PresentacionFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar presentaciones");

    public Task<Result<IReadOnlyList<PresentacionDto>>> BuscarSugerenciasAsync(
        string termino, PresentacionFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias presentaciones");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, PresentacionFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de presentación");

    // ── Escritura ─────────────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(PresentacionDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nueva  = new Pres
            {
                nombrePresentacion      = dto.Nombre,
                descripcionPresentacion = dto.Descripcion,
                idEstado                = dto.IdEstado,
            };
            var resultado = await client.From<Pres>().Insert(nueva);
            return resultado.Models.First().idPresentacion;
        }, "Crear presentación");

    public Task<Result> UpdateAsync(PresentacionDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Pres>()
                .Where(p => p.idPresentacion == dto.Id)
                .Set(p => p.nombrePresentacion,      dto.Nombre)
                .Set(p => p.descripcionPresentacion, dto.Descripcion)
                .Set(p => p.idEstado,                dto.IdEstado)
                .Update();
        }, "Actualizar presentación");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<Pres>()
                .Where(p => p.idPresentacion == id)
                .Set(p => p.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar presentación");

    // ── Lógica interna ────────────────────────────────────────────────────────

    private async Task<PagedResult<PresentacionDto>> GetPagedInternal(
        int page, int size, PresentacionFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<Pres>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("id_presentacion", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosRpcAsync(filtros, client);
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
        var query  = AplicarFiltros(client.From<Pres>().Select("*"), filtros);

        var resultado = await query
            .Or(new List<IPostgrestQueryFilter>
            {
                new QueryFilter("nombre_presentacion",      Op.ILike, $"%{termino}%"),
                new QueryFilter("descripcion_presentacion", Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_presentacion", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, PresentacionFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<Pres>()
                  .Select("id_presentacion")
                  .Filter("id_presentacion", Op.LessThan, id.ToString()),
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
        PresentacionFiltros filtros, Supabase.Client client)
    {
        var parametros = new Dictionary<string, object?>();
        if (filtros.IdEstado.HasValue) parametros["p_estado"] = filtros.IdEstado.Value;

        var response = await client.Rpc("contar_presentaciones", parametros);
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
