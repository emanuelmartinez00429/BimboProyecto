namespace CapaAplicacion.Usuarios.Dtos;

/// <summary>
/// Datos para crear un usuario via RPC crear_usuario_empleado_seguro.
/// </summary>
public sealed class CrearUsuarioDto
{
    public int    IdEmpleado { get; init; }
    public string Email      { get; init; } = string.Empty;
    public string Password   { get; init; } = string.Empty;
    public int    IdRol      { get; init; }
}
