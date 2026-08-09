namespace CapaAplicacion.Usuarios.Dtos;

/// <summary>
/// DTO de rol para interfaces de CapaAplicacion.
/// Mapeado desde CapaDatos.Modelados.Usuarios.Roles.
/// </summary>
public sealed class RolDto
{
    public int    IdRol     { get; init; }
    public string NombreRol { get; init; } = string.Empty;
}
