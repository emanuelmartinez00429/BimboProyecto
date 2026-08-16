using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Interfaces;
using CapaAplicacion.Empleados.Queries;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

// Namespace NO puede llamarse "...Repositories.Empleados": el modelo Supabase
// CapaDatos.Modelados.Usuarios.Empleados se referencia sin calificar en otros
// archivos (EmpleadoRepository.cs del buscador universal, UsuarioRepository.cs)
// vía "using CapaDatos.Modelados.Usuarios;" — un namespace hermano con el mismo
// nombre gana la resolución sobre el using y rompe esos archivos (CS0118).
namespace CapaDatos.Repositories.GestionEmpleados;

public class EmpleadoCrudRepository : RepositorioBase, IEmpleadoRepository
{
    private readonly IUsuarioSesionService _sesionService;

    public EmpleadoCrudRepository(
        IConexionMonitor conexion,
        IUsuarioSesionService sesionService) : base(conexion)
    {
        _sesionService = sesionService;
    }

    private static EmpleadoDto Map(CapaDatos.Modelados.Usuarios.Empleados e) => new()
    {
        IdEmpleado       = e.idEmpleado,
        NombreEmpleado   = e.nombreEmpleado   ?? string.Empty,
        ApellidoEmpleado = e.apellidoEmpleado ?? string.Empty,
        NumeroIdentidad  = e.numeroIdentidad  ?? string.Empty,
        TelefonoEmpleado = e.telefonoEmpleado ?? string.Empty,
        CorreoEmpleado   = e.correoEmpleado   ?? string.Empty,
        IdEstado         = e.idEstado,
    };

    // ── Lectura ─────────────────────────────────────────────────────────────

    public Task<Result<PagedResult<EmpleadoDto>>> GetPagedAsync(
        int page, int size, EmpleadoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPagedInternal(page, size, filtros), "Cargar empleados");

    public Task<Result<IReadOnlyList<EmpleadoDto>>> BuscarSugerenciasAsync(
        string termino, EmpleadoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => BuscarSugerenciasInternal(termino, filtros), "Buscar sugerencias empleados");

    public Task<Result<int>> GetPaginaDeRegistroAsync(
        int id, int size, EmpleadoFiltros filtros, CancellationToken ct = default) =>
        TryAsync(() => GetPaginaDeRegistroInternal(id, size, filtros), "Calcular página de empleado");

    // ── Escritura ──────────────────────────────────────────────────────

    public Task<Result<int>> CreateAsync(EmpleadoDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException(
                    "No hay una sesión activa; no se puede crear el empleado.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_dni"]                = dto.NumeroIdentidad,
                ["p_nombre_empleado"]    = dto.NombreEmpleado,
                ["p_apellido_empleado"]  = dto.ApellidoEmpleado,
                ["p_numero_telefonico"]  = dto.TelefonoEmpleado,
                ["p_correo_empleado"]    = dto.CorreoEmpleado,
                ["p_estado_empleado"]    = dto.IdEstado,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc(
                "ingresar_empleado_tabla_bitacora",
                parametros);

            ct.ThrowIfCancellationRequested();

            string? json = response?.Content;
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException(
                    "La función de creación no devolvió el identificador del empleado.");

            int idEmpleado;
            try
            {
                idEmpleado = JToken.Parse(json).ToObject<int>();
            }
            catch (Exception ex) when (ex is JsonException or FormatException)
            {
                throw new InvalidOperationException(
                    "La función de creación devolvió un identificador inválido.", ex);
            }

            if (idEmpleado <= 0)
                throw new InvalidOperationException(
                    "La función de creación devolvió un identificador inválido.");

            return idEmpleado;
        }, "Crear empleado");

    public Task<Result> UpdateAsync(EmpleadoDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<CapaDatos.Modelados.Usuarios.Empleados>()
                .Where(e => e.idEmpleado == dto.IdEmpleado)
                .Set(e => e.nombreEmpleado,   dto.NombreEmpleado)
                .Set(e => e.apellidoEmpleado, dto.ApellidoEmpleado)
                .Set(e => e.numeroIdentidad,  dto.NumeroIdentidad)
                .Set(e => e.telefonoEmpleado, dto.TelefonoEmpleado)
                .Set(e => e.correoEmpleado,   dto.CorreoEmpleado)
                .Set(e => e.idEstado,         dto.IdEstado)
                .Update();
        }, "Actualizar empleado");

    public Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<CapaDatos.Modelados.Usuarios.Empleados>()
                .Where(e => e.idEmpleado == id)
                .Set(e => e.idEstado, nuevoEstado)
                .Update();
        }, "Cambiar estado empleado");

    // ── Lógica interna ──────────────────────────────────────────────────────

    private async Task<PagedResult<EmpleadoDto>> GetPagedInternal(int page, int size, EmpleadoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<CapaDatos.Modelados.Usuarios.Empleados>().Select("*"), filtros);

        int from = (page - 1) * size;
        int to   = from + size - 1;

        var pageTask    = query.Order("id_empleado", Ord.Ascending).Range(from, to).Get();
        var conteosTask = GetConteosAsync(client, filtros);
        await Task.WhenAll(pageTask, conteosTask);

        var items   = pageTask.Result?.Models.Select(Map).ToList() ?? [];
        var conteos = conteosTask.Result;

        return new PagedResult<EmpleadoDto>
        {
            Items     = items,
            Total     = conteos.total,
            Activos   = conteos.activos,
            Inactivos = conteos.inactivos,
        };
    }

    private async Task<IReadOnlyList<EmpleadoDto>> BuscarSugerenciasInternal(string termino, EmpleadoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(client.From<CapaDatos.Modelados.Usuarios.Empleados>().Select("*"), filtros);

        var resultado = await query
            .Or(new List<Supabase.Postgrest.Interfaces.IPostgrestQueryFilter>
            {
                new Supabase.Postgrest.QueryFilter("nombre_empleado",   Op.ILike, $"%{termino}%"),
                new Supabase.Postgrest.QueryFilter("apellido_empleado", Op.ILike, $"%{termino}%"),
                new Supabase.Postgrest.QueryFilter("numero_identidad",  Op.ILike, $"%{termino}%"),
            })
            .Order("nombre_empleado", Ord.Ascending)
            .Limit(10)
            .Get();

        return resultado?.Models.Select(Map).ToList() ?? [];
    }

    private async Task<int> GetPaginaDeRegistroInternal(int id, int size, EmpleadoFiltros filtros)
    {
        var client = await ConexionSupabase.GetClientAsync();
        var query  = AplicarFiltros(
            client.From<CapaDatos.Modelados.Usuarios.Empleados>()
                  .Select("id_empleado")
                  .Filter("id_empleado", Op.LessThan, id.ToString()),
            filtros);

        var result  = await query.Get();
        int previos = result?.Models.Count ?? 0;
        return (previos / size) + 1;
    }

    private static Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Usuarios.Empleados> AplicarFiltros(
        Supabase.Postgrest.Interfaces.IPostgrestTable<CapaDatos.Modelados.Usuarios.Empleados> query, EmpleadoFiltros filtros)
    {
        if (filtros.IdEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
        return query;
    }

    private static async Task<(int total, int activos, int inactivos)> GetConteosAsync(
        Supabase.Client client, EmpleadoFiltros filtros)
    {
        var query = AplicarFiltros(
            client.From<CapaDatos.Modelados.Usuarios.Empleados>().Select("id_empleado, id_estado"),
            filtros);

        var resultado = await query.Get();
        var models    = resultado?.Models ?? new List<CapaDatos.Modelados.Usuarios.Empleados>();

        int total   = models.Count;
        int activos = models.Count(e => e.idEstado == 1);
        return (total, activos, total - activos);
    }
}
