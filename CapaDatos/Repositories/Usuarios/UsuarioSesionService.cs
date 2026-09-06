using CapaAplicacion.Common;
using CapaAplicacion.Perfil;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using CapaDominio.Entities;
using ServicioConexión.Conexion;
using UsuariosModel = CapaDatos.Modelados.Usuarios.Usuarios;
using Op = Supabase.Postgrest.Constants.Operator;

namespace CapaDatos.Repositories.Usuarios;

/// <summary>
/// Servicio de sesion del usuario. Singleton que mantiene el
/// UsuarioSesion actual (perfil + permisos reales de BD).
/// </summary>
public class UsuarioSesionService : IUsuarioSesionService
{
    private readonly IPerfilUsuarioService _perfilService;
    private UsuarioSesion? _sesion;

    public UsuarioSesionService(IPerfilUsuarioService perfilService)
    {
        _perfilService = perfilService;
    }

    public UsuarioSesion? SesionActual => _sesion;
    public bool Autenticado => _sesion is not null;

    public async Task<Result<UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default)
    {
        try
        {
            var client = await ConexionSupabase.GetClientAsync();

            // 1) Cargar perfil (reutiliza servicio existente)
            await _perfilService.CargarAsync(idUsuario);
            var perfil = _perfilService.PerfilActual;

            // 2) Obtener usuario con rol
            var usuarioRes = await client
                .From<usuarioVista>()
                .Select("*, roles(*)")
                .Filter("id_usuario", Op.Equals, idUsuario.ToString())
                .Get();
            var usuario = usuarioRes?.Models?.FirstOrDefault();
            if (usuario is null)
                return Result<UsuarioSesion>.Fail("Usuario no encontrado.");

            // 3) Actualizar ultimo_acceso (best-effort, no bloquea login)
            _ = ActualizarUltimoAccesoAsync(client, idUsuario);

            // 4) Cargar permisos reales (3 queries en paralelo)
            var idsAccionesTask = CargarIdsAccionesAsync(client, usuario.idRol);
            var accionesTask    = client.From<Accion>().Get();
            var modulosTask     = client.From<Modulo>().Get();

            await Task.WhenAll(idsAccionesTask, accionesTask, modulosTask);

            var idsAcciones = await idsAccionesTask;
            var acciones    = accionesTask.Result?.Models  ?? new List<Accion>();
            var modulos     = modulosTask.Result?.Models   ?? new List<Modulo>();

            // 5) Agrupar permisos por modulo
            var permisosPorModulo = UsuarioSesionServiceHelper
                .ConstruirPermisosPorModulo(acciones, modulos, idsAcciones);

            // 6) Construir sesion
            _sesion = new UsuarioSesion(permisosPorModulo)
            {
                IdUsuario      = usuario.idUsuario,
                Email          = usuario.aliasUsuario ?? string.Empty,
                IdRol          = usuario.idRol,
                NombreRol      = perfil?.NombreRol ?? usuario.roles?.nombreRol ?? string.Empty,
                NombreEmpleado = perfil?.NombreEmpleado ?? string.Empty,
                ApellidoEmpleado = perfil?.ApellidoEmpleado ?? string.Empty,
                NombreCompleto = perfil?.NombreCompleto ?? string.Empty,
                Iniciales      = perfil?.Iniciales ?? string.Empty,
            };

            return Result<UsuarioSesion>.Ok(_sesion);
        }
        catch (Exception ex)
        {
            return Result<UsuarioSesion>.Fail($"Error al iniciar sesion: {ex.Message}");
        }
    }

    public void CerrarSesion()
    {
        _sesion = null;
        _perfilService.Limpiar();
    }

    public bool TienePermiso(string nombreAccion)
        => _sesion?.TieneAccion(nombreAccion) ?? false;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<int>> CargarIdsAccionesAsync(
        Supabase.Client client, int idRol)
    {
        var res = await client
            .From<AccionRol>()
            .Select("id_accion")
            .Filter("id_rol",   Op.Equals, idRol.ToString())
            .Filter("id_estado", Op.Equals, "1")
            .Get();

        return res?.Models?.Select(a => a.idAccion).ToList() ?? new List<int>();
    }

    private static async Task ActualizarUltimoAccesoAsync(Supabase.Client client, int idUsuario)
    {
        try
        {
            await client.From<UsuariosModel>()
                .Where(u => u.idUsuario == idUsuario)
                .Set(u => u.ultimoAcceso!, DateTime.UtcNow)
                .Update();
        }
        catch
        {
            // No bloquea el login
        }
    }
}

/// <summary>
/// Helper estatico para construir permisos por modulo.
/// Separado para poderse testear independientemente.
/// </summary>
internal static class UsuarioSesionServiceHelper
{
    public static IReadOnlyList<ModuloPermisos> ConstruirPermisosPorModulo(
        IReadOnlyList<Accion> acciones,
        IReadOnlyList<Modulo> modulos,
        IEnumerable<int> idsAccionesAsignadas)
    {
        var idsSet = idsAccionesAsignadas.ToHashSet();
        var accionesAsignadas = acciones.Where(a => idsSet.Contains(a.idAccion)).ToList();

        var modulosMap = modulos.ToDictionary(m => m.idModulo);
        var resultado  = new Dictionary<int, (string nombre, string? desc, List<string> acciones)>();

        foreach (var accion in accionesAsignadas)
        {
            if (!modulosMap.TryGetValue(accion.idModulo, out var modulo))
                continue;

            if (!resultado.ContainsKey(accion.idModulo))
                resultado[accion.idModulo] = (modulo.nombreModulo, modulo.descripcionModulo, new());

            resultado[accion.idModulo].acciones.Add(accion.codigoAccion);
        }

        return resultado
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new ModuloPermisos
            {
                IdModulo          = kvp.Key,
                NombreModulo      = kvp.Value.nombre,
                DescripcionModulo = kvp.Value.desc,
                Acciones          = kvp.Value.acciones.AsReadOnly(),
            })
            .ToList();
    }
}
