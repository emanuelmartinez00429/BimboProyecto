namespace CapaAplicacion.Usuarios.Dtos;

/// <summary>
/// Datos para actualizar un usuario existente.
/// Solo se aplican los campos con valor (no nulos).
/// </summary>
public sealed class ActualizarUsuarioDto
{
    public int     IdUsuario { get; init; }
    public int?    IdRol     { get; init; }
    public int?    IdEstado  { get; init; }
    public string? Email     { get; init; }
}
