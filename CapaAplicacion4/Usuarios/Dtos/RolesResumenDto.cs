namespace CapaAplicacion.Usuarios.Dtos;

/// <summary>
/// Fotografía completa del subsistema RBAC en una sola llamada: roles, catálogo
/// de módulos/acciones y asignaciones de TODOS los roles.
///
/// Se trae todo junto a propósito: la pantalla de Roles cambia de rol
/// constantemente y, con esto, ese cambio no toca la red ni una sola vez.
/// </summary>
public sealed class RolesResumenDto
{
    public IReadOnlyList<RolDto> Roles { get; init; } = Array.Empty<RolDto>();

    public IReadOnlyList<ModuloAccionesDto> Modulos { get; init; } = Array.Empty<ModuloAccionesDto>();

    /// <summary>id_rol → ids de acciones activas para ese rol.</summary>
    public IReadOnlyDictionary<int, IReadOnlySet<int>> AccionesPorRol { get; init; } =
        new Dictionary<int, IReadOnlySet<int>>();
}
