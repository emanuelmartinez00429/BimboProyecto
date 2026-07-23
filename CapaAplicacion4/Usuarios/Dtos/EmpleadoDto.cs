namespace CapaAplicacion.Usuarios.Dtos;

public sealed class EmpleadoDto
{
    public int    IdEmpleado       { get; init; }
    public string NombreEmpleado   { get; init; } = string.Empty;
    public string ApellidoEmpleado { get; init; } = string.Empty;
    public string CorreoEmpleado   { get; init; } = string.Empty;
}
