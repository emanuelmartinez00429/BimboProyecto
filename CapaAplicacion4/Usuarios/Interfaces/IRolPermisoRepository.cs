using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;

namespace CapaAplicacion.Usuarios.Interfaces;

public interface IRolPermisoRepository
{
    Task<Result<IReadOnlyList<ModuloAccionesDto>>> ObtenerCatalogoAsync(
        CancellationToken ct = default);

    Task<Result<IReadOnlySet<int>>> ObtenerAccionesAsignadasAsync(
        int idRol,
        CancellationToken ct = default);

    Task<Result> GuardarAsignacionesAsync(
        int idRol,
        IReadOnlyCollection<int> idsAcciones,
        CancellationToken ct = default);
}
