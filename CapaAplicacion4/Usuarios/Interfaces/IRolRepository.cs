using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;

namespace CapaAplicacion.Usuarios.Interfaces;

/// <summary>
/// Consulta y administración del catálogo de roles.
/// </summary>
public interface IRolRepository
{
    Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(bool incluirInactivos = false, CancellationToken ct = default);
    Task<Result<RolDto>> CrearAsync(string nombreRol, CancellationToken ct = default);
    Task<Result<RolDto>> ActualizarAsync(int idRol, string nombreRol, CancellationToken ct = default);
    Task<Result<RolDto>> CambiarEstadoAsync(int idRol, int idEstado, CancellationToken ct = default);
}
