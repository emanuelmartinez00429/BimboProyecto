using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;

namespace CapaAplicacion.Usuarios.Interfaces;

/// <summary>
/// Repositorio de roles. Solo lectura — utilizado para
/// poblar ComboBox de seleccion de rol en el modal de usuario.
/// </summary>
public interface IRolRepository
{
    Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default);
}
