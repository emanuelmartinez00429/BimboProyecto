namespace CapaAplicacion.Reportes.Dtos;

public sealed class ReportAuthorDto
{
    public string Email            { get; init; } = string.Empty;
    public string NombreEmpleado   { get; init; } = string.Empty;
    public string ApellidoEmpleado { get; init; } = string.Empty;
    public string Rol              { get; init; } = string.Empty;
}
