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
    public UsuarioRepository(IConexionMonitor conexion) : base(conexion) { }

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
                query = query.Or(new List<Supabase.Postgrest.Interfaces.IPostgrestQueryFilter>
                {
                    new Supabase.Postgrest.QueryFilter("alias_usuario", Op.ILike, $"%{busqueda}%"),
                });

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

    public Task<Result> CrearAsync(CrearUsuarioDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();

            // 1) Crear auth user usando un cliente TEMPORAL
            //    para NO destruir la sesión del admin logueado.
            Supabase.Client? tempClient = null;
            try
            {
                string? url = Environment.GetEnvironmentVariable("SUPABASE_URL")
                    ?? System.Configuration.ConfigurationManager.AppSettings["SUPABASE_URL"];
                string? key = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                    ?? System.Configuration.ConfigurationManager.AppSettings["SUPABASE_KEY"];

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("SUPABASE_URL / SUPABASE_KEY no configurados.");

                tempClient = new Supabase.Client(url, key, new Supabase.SupabaseOptions
                {
                    AutoConnectRealtime = false,
                    AutoRefreshToken    = false,
                });
                await tempClient.InitializeAsync();

                var sessionBefore = client.Auth.CurrentSession;
                System.Diagnostics.Debug.WriteLine(
                    $"[CrearAsync] Sesión ANTES del SignUp: email={sessionBefore?.User?.Email}, token={sessionBefore?.AccessToken?[..Math.Min(20, sessionBefore.AccessToken?.Length ?? 0)]}...");

                await tempClient.Auth.SignUp(dto.Email, dto.Password);

                // Restaurar sesión del admin en el singleton (por si SignUp la afectó)
                if (sessionBefore?.AccessToken is not null)
                    await client.Auth.SetSession(sessionBefore.AccessToken, sessionBefore.RefreshToken);

                System.Diagnostics.Debug.WriteLine(
                    $"[CrearAsync] Sesión DESPUÉS del SignUp: email={client.Auth.CurrentSession?.User?.Email}");
            }
            catch (Exception ex)
            {
                // Si el auth user ya existe, no es error — la RPC lo maneja
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
                // Limpiar el cliente temporal
                if (tempClient is not null)
                {
                    try
                    {
                        await tempClient.Auth.SignOut();
                        (tempClient.Realtime.Socket as IDisposable)?.Dispose();
                    }
                    catch { /* cleanup best-effort */ }
                }
            }

            // 2) Debug: verificar sesión antes de la RPC
            var session = client.Auth.CurrentSession;
            System.Diagnostics.Debug.WriteLine(
                $"[CrearAsync] Sesión actual ANTES de RPC: email={session?.User?.Email}, token={session?.AccessToken?[..Math.Min(30, session?.AccessToken?.Length ?? 0)]}...");

            // 3) Llamar a la RPC que vincula auth user con empleado
            var response = await client.Rpc("crear_usuario_empleado_seguro", new
            {
                p_id_empleado = dto.IdEmpleado,
                p_email       = dto.Email,
                p_rol         = dto.IdRol,
            });

            var resultado = response?.Content?.Trim('"') ?? string.Empty;

            switch (resultado)
            {
                case "USUARIO_CREADO":
                    return; // éxito

                case "USUARIO_NO_EXISTE":
                    throw new InvalidOperationException("El empleado especificado no existe en el sistema.");

                case "USUARIO_YA_EXISTE":
                    throw new InvalidOperationException("El empleado ya tiene un usuario asociado.");

                default:
                    throw new InvalidOperationException($"Respuesta inesperada del servidor: {resultado}");
            }
        }, "Crear usuario");

    public Task<Result> ActualizarAsync(ActualizarUsuarioDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var query = client.From<UsuariosModel>()
                .Where(u => u.idUsuario == dto.IdUsuario);

            if (dto.IdRol.HasValue)
                query = query.Set(u => u.idRol, dto.IdRol.Value);
            if (dto.IdEstado.HasValue)
                query = query.Set(u => u.idEstado, dto.IdEstado.Value);
            if (dto.Email is not null)
                query = query.Set(u => u.correoUsuario, dto.Email);

            var response = await query.Update();

            // Supabase devuelve 200 OK con Models vacío cuando RLS bloquea el UPDATE.
            // Verificar que al menos 1 fila fue afectada.
            if (response?.Models is null || response.Models.Count == 0)
                throw new InvalidOperationException(
                    "No se pudo actualizar el registro. Verifique los permisos de la tabla 'usuarios'.");
        }, "Actualizar usuario");

    public Task<Result> CambiarEstadoAsync(int idUsuario, int idEstado, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.From<UsuariosModel>()
                .Where(u => u.idUsuario == idUsuario)
                .Set(u => u.idEstado, idEstado)
                .Update();

            if (response?.Models is null || response.Models.Count == 0)
                throw new InvalidOperationException(
                    "No se pudo cambiar el estado del usuario. Verifique los permisos de la tabla 'usuarios'.");
        }, "Cambiar estado de usuario");

    // ── Mapping ─────────────────────────────────────────────────────────────

    private static UsuarioVistaDto MapToDto(usuarioVista u)
    {
        // ── Diagnóstico FK ──
        var empName = u.empleados?.nombreEmpleado;
        var empLast = u.empleados?.apellidoEmpleado;
        var rolName = u.roles?.nombreRol;

        System.Diagnostics.Debug.WriteLine(
            $"[MapToDto] id={u.idUsuario} | empleados={(u.empleados is null ? "NULL" : $"OK (nombre={empName}, apellido={empLast})")} " +
            $"| roles={(u.roles is null ? "NULL" : $"OK (nombre={rolName})")}");

        return new UsuarioVistaDto
        {
            IdUsuario     = u.idUsuario,
            CorreoUsuario = u.correoUsuario,
            IdEmpleado    = u.idEmpleado,
            IdRol         = u.idRol,
            IdEstado      = u.idEstado,
            UuidUsuario   = u.uuidUsuario,
            UltimoAcceso  = u.ultimoAcceso,
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
        var query = client.From<UsuariosModel>().Select("id_usuario, id_estado");

        if (idEstado.HasValue)
            query = query.Filter("id_estado", Op.Equals, idEstado.Value.ToString());
        if (idRol.HasValue)
            query = query.Filter("id_rol", Op.Equals, idRol.Value.ToString());
        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Filter("alias_usuario", Op.ILike, $"%{busqueda}%");

        var resultado = await query.Get();
        var models    = resultado?.Models ?? new List<UsuariosModel>();

        int total   = models.Count;
        int activos = models.Count(u => u.idEstado == 1);
        return (total, activos, total - activos);
    }
}
