namespace CapaAplicacion.Usuarios.Dtos;

public sealed class UsuarioVistaDto
{
    public int      IdUsuario     { get; init; }
    public string   CorreoUsuario { get; init; } = string.Empty;
    public int      IdEmpleado    { get; init; }
    public int      IdRol         { get; init; }
    public int      IdEstado      { get; init; }
    public string?  UuidUsuario   { get; init; }
    public DateTime? UltimoAcceso  { get; init; }
    public string   NombreRol     { get; init; } = string.Empty;
    public string   NombreEmpleado { get; init; } = string.Empty;
}
