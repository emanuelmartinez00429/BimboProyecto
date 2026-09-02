using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Usuarios.Dtos;

namespace CapaAplicacion.Usuarios.Interfaces;

/// <summary>
/// Repositorio de usuarios. CRUD completo, paginacion, busqueda
/// y operaciones de sesion (RPC de creacion).
/// </summary>
public interface IUsuarioRepository
{
    // ── Lectura ──────────────────────────────────────────────────────────
    Task<Result<UsuarioVistaDto>>                ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default);
    Task<Result<UsuarioVistaDto>>                ObtenerPorUuidAsync(string uuid, CancellationToken ct = default);
    Task<Result<PagedResult<UsuarioVistaDto>>>   ObtenerPaginaAsync(int page, int pageSize, int? idEstado, int? idRol, string? busqueda, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmpleadoDto>>>     ObtenerEmpleadosSinUsuarioAsync(CancellationToken ct = default);

    // ── Escritura ────────────────────────────────────────────────────────
    Task<Result>                                 CrearAsync(CrearUsuarioDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>                                 ActualizarAsync(ActualizarUsuarioDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>                                 CambiarEstadoAsync(int idUsuario, int idEstado, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>                                 AsignarRolAsync(int idUsuario, int idRol, Guid idSolicitud, CancellationToken ct = default);
}
