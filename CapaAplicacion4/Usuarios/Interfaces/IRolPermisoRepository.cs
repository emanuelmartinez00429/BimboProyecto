using CapaAplicacion.Common;
using CapaAplicacion.Usuarios.Dtos;

namespace CapaAplicacion.Usuarios.Interfaces;

public interface IRolPermisoRepository
{
    /// <summary>
    /// Carga de un solo viaje todo lo que necesita la pantalla de Roles.
    /// Evita re-consultar la BD cada vez que el usuario cambia de rol.
    /// </summary>
    Task<Result<RolesResumenDto>> ObtenerResumenAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<ModuloAccionesDto>>> ObtenerCatalogoAsync(
        CancellationToken ct = default);

    Task<Result<IReadOnlySet<int>>> ObtenerAccionesAsignadasAsync(
        int idRol,
        CancellationToken ct = default);

    Task<Result> GuardarAsignacionesAsync(
        int idRol,
        IReadOnlyCollection<int> idsAcciones,
        CancellationToken ct = default);

    /// <summary>
    /// Invalida las definiciones de RBAC cacheadas en memoria.
    /// Invocado durante la limpieza de sesión para evitar filtraciones entre usuarios.
    /// </summary>
    void PurgarCache();
}
