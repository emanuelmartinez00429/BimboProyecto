using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using UsuariosModel = CapaDatos.Modelados.Usuarios.Usuarios;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Usuarios;

public class UsuarioRepository : RepositorioBase, IUsuarioRepository
{
    private readonly IUsuarioSesionService _sesionService;

    public UsuarioRepository(
        IConexionMonitor conexion,
        IUsuarioSesionService sesionService) : base(conexion)
    {
        _sesionService = sesionService;
    }

    // ── Lectura ──────────────────────────────────────────────────────────────

    public Task<Result<UsuarioVistaDto>> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var resultado = await client
                .From<usuarioVista>()
                .Select("*, roles(*), empleados(*)")
                .Filter("id_usuario", Op.Equals, idUsuario.ToString())
                .Get();
            var model = resultado?.Models?.FirstOrDefault()
                ?? throw new InvalidOperationException("Usuario no encontrado.");
            return MapToDto(model);
        }, "Obtener usuario por ID");

    public Task<Result<UsuarioVistaDto>> ObtenerPorUuidAsync(string uuid, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var resultado = await client
                .From<usuarioVista>()
                .Select("*, roles(*), empleados(*)")
                .Filter("uuid_usuario", Op.Equals, uuid)
                .Get();
            var model = resultado?.Models?.FirstOrDefault()
                ?? throw new InvalidOperationException("Usuario no encontrado por UUID.");
            return MapToDto(model);
        }, "Obtener usuario por UUID");

    public Task<Result<PagedResult<UsuarioVistaDto>>> ObtenerPaginaAsync(
        int page, int pageSize, int? idEstado, int? idRol, string? busqueda, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            // ── Query de items ──
            var query = client.From<usuarioVista>().Select("*, roles(*), empleados(*)");

            if (idEstado.HasValue)
                query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());
            if (idRol.HasValue)
                query = query.Filter("id_rol", Op.Equals, idRol.Value.ToString());
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var aguja = TextoBusqueda.Normalizar(busqueda);
                query = query.Filter("busqueda_usuario", Op.ILike, $"%{aguja}%");
            }

            int from = (page - 1) * pageSize;
            int to   = from + pageSize - 1;

            var pageTask = query
                .Order("id_usuario", Ord.Ascending)
                .Range(from, to)
                .Get();

            // ── Conteos ──
            var conteosTask = ObtenerConteosAsync(client, idEstado, idRol, busqueda);

            await Task.WhenAll(pageTask, conteosTask);

            var items   = pageTask.Result?.Models?.ToList() ?? [];
            var conteos = conteosTask.Result;

            return new PagedResult<UsuarioVistaDto>
            {
                Items     = items.Select(MapToDto).ToList(),
                Total     = conteos.total,
                Activos   = conteos.activos,
                Inactivos = conteos.inactivos,
            };
        }, "Cargar pagina de usuarios");

    public Task<Result<IReadOnlyList<EmpleadoDto>>> ObtenerEmpleadosSinUsuarioAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            // 1) Empleados activos
            var empleadosRes = await client
                .From<Empleados>()
                .Filter("id_estado", Op.Equals, "1")
                .Order("nombre_empleado", Ord.Ascending)
                .Get();
            var todos = empleadosRes?.Models ?? new List<Empleados>();

            // 2) IDs de empleados que ya tienen usuario
            var usuariosRes = await client
                .From<UsuariosModel>()
                .Select("id_empleado")
                .Get();
            var idsConUsuario = usuariosRes?.Models?
                .Select(u => u.idEmpleado)
                .ToHashSet() ?? new HashSet<int>();

            // 3) Filtrar y mapear a DTO
            return (IReadOnlyList<EmpleadoDto>)todos
                .Where(e => !idsConUsuario.Contains(e.idEmpleado))
                .Select(MapToDto)
                .ToList();
        }, "Obtener empleados sin usuario");

    // ── Escritura ────────────────────────────────────────────────────────────

    public Task<Result> CrearAsync(CrearUsuarioDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            string? url = Environment.GetEnvironmentVariable("SUPABASE_URL") ?? System.Configuration.ConfigurationManager.AppSettings["SUPABASE_URL"];
            string? key = Environment.GetEnvironmentVariable("SUPABASE_KEY") ?? System.Configuration.ConfigurationManager.AppSettings["SUPABASE_KEY"];

            var tempClient = new Supabase.Client(
                url!,
                key!,
                new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = false,
                    AutoConnectRealtime = false
                });

            await tempClient.InitializeAsync();
            try
            {
                var sesionNueva = await tempClient.Auth.SignUp(dto.Email, dto.Password);
            }
            catch (Exception ex)
            {
                if (!ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
                    && !ex.Message.Contains("ya existe", StringComparison.OrdinalIgnoreCase)
                    && !ex.Message.Contains("User already registered", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Error al crear cuenta de autenticación: {ex.Message}");
                }
            }
            finally
            {
                if (tempClient is not null)
                {
                    try
                    {
                        await tempClient.Auth.SignOut();
                        (tempClient.Realtime.Socket as IDisposable)?.Dispose();
                    }
                    catch { }
                }
            }

            var response = await client.Rpc("crear_usuario_empleado_seguro", new Dictionary<string, object?>
            {
                ["p_id_empleado"] = dto.IdEmpleado,
                ["p_email"]       = dto.Email,
                ["p_rol"]         = dto.IdRol,
                ["p_id_solicitud"] = idSolicitud
            });

            var json = response?.Content;
            if (!string.IsNullOrWhiteSpace(json))
            {
                var token = Newtonsoft.Json.Linq.JToken.Parse(json);
                if (token is Newtonsoft.Json.Linq.JObject obj && obj.TryGetValue("error", out var errToken))
                {
                    var err = (string?)errToken;
                    if (err == "USUARIO_NO_EXISTE")
                        throw new InvalidOperationException("El empleado especificado no existe en el sistema.");
                    if (err == "USUARIO_YA_EXISTE")
                        throw new InvalidOperationException("El empleado ya tiene un usuario asociado.");
                }
            }
        }, "Crear usuario");

    public Task<Result> ActualizarAsync(ActualizarUsuarioDto dto, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirUsuarioObjetivoDistinto(dto.IdUsuario);
            var client = await ConexionSupabase.GetClientAsync();
            await client.Rpc("actualizar_usuario_seguro", new Dictionary<string, object?>
            {
                ["p_id_usuario"] = dto.IdUsuario,
                ["p_id_rol"] = dto.IdRol,
                ["p_id_estado"] = dto.IdEstado,
                ["p_email"] = dto.Email,
                ["p_id_solicitud"] = idSolicitud
            });
        }, "Actualizar usuario");

    public Task<Result> CambiarEstadoAsync(int idUsuario, int idEstado, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirUsuarioObjetivoDistinto(idUsuario);
            var client = await ConexionSupabase.GetClientAsync();
            await client.Rpc("cambiar_estado_usuario_seguro", new Dictionary<string, object?>
            {
                ["p_id_usuario"] = idUsuario,
                ["p_id_estado"] = idEstado,
                ["p_id_solicitud"] = idSolicitud
            });
        }, "Cambiar estado de usuario");

    public Task<Result> AsignarRolAsync(int idUsuario, int idRol, Guid idSolicitud, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirUsuarioObjetivoDistinto(idUsuario);
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            await client.Rpc("asignar_rol_usuario_seguro", new Dictionary<string, object?>
            {
                ["p_id_usuario"] = idUsuario,
                ["p_id_rol"] = idRol,
                ["p_id_solicitud"] = idSolicitud,
            });
            ct.ThrowIfCancellationRequested();
        }, "Asignar rol a usuario");

    private void ExigirUsuarioObjetivoDistinto(int idUsuarioObjetivo)
    {
        var sesion = _sesionService.SesionActual
            ?? throw new UnauthorizedAccessException("No existe una sesión activa.");

        if (sesion.IdUsuario == idUsuarioObjetivo)
            throw new UnauthorizedAccessException(
                "No puedes cambiar tu propio rol ni deshabilitar tu cuenta desde este módulo.");
    }

    // ── Mapping ─────────────────────────────────────────────────────────────

    private static UsuarioVistaDto MapToDto(usuarioVista u)
    {
        var empName = u.empleados?.nombreEmpleado;
        var empLast = u.empleados?.apellidoEmpleado;
        var rolName = u.roles?.nombreRol;

        return new UsuarioVistaDto
        {
            IdUsuario     = u.idUsuario,
            CorreoUsuario = u.aliasUsuario,
            IdEmpleado    = u.idEmpleado,
            IdRol         = u.idRol,
            IdEstado      = u.idEstado,
            UuidUsuario   = u.uuidUsuario,
            UltimoAcceso  = u.ultimoAcceso,
            CreatedAt     = u.createdAt,
            UpdatedAt     = u.updatedAt,
            NombreRol     = rolName ?? "Sin rol",
            NombreEmpleado = u.empleados != null
                ? $"{empName} {empLast}".Trim()
                : "Sin empleado",
        };
    }

    private static EmpleadoDto MapToDto(Empleados e) => new()
    {
        IdEmpleado       = e.idEmpleado,
        NombreEmpleado   = e.nombreEmpleado,
        ApellidoEmpleado = e.apellidoEmpleado,
        CorreoEmpleado   = e.correoEmpleado,
    };

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(int total, int activos, int inactivos)> ObtenerConteosAsync(
        Supabase.Client client, int? idEstado, int? idRol, string? busqueda)
    {
        // Misma vista que la página para que los conteos coincidan con el filtro
        // de búsqueda por alias O nombre de empleado (P-021).
        var query = client.From<usuarioVista>().Select("id_usuario, id_estado");

        if (idEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());
        if (idRol.HasValue)
            query = query.Filter("id_rol", Op.Equals, idRol.Value.ToString());
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var aguja = TextoBusqueda.Normalizar(busqueda);
            query = query.Filter("busqueda_usuario", Op.ILike, $"%{aguja}%");
        }

        var resultado = await query.Get();
        var models    = resultado?.Models ?? new List<usuarioVista>();

        int total   = models.Count;
        int activos = models.Count(u => u.idEstado == 1);
        return (total, activos, total - activos);
    }
}
