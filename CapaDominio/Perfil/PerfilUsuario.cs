namespace CapaDominio;

public class PerfilUsuario
{
    public string NombreEmpleado   { get; set; } = "";
    public string ApellidoEmpleado { get; set; } = "";
    public string NombreCompleto { get; set; } = "";
    public string Iniciales      { get; set; } = "";
    public string NombreUsuario  { get; set; } = "";
    public string Correo         { get; set; } = "";
    public string NombreRol      { get; set; } = "";

    /// <summary>
    /// Apodo personal guardado como preferencia (clave 'apodo'), o <c>null</c> cuando
    /// el usuario no definió uno y <see cref="NombreCompleto"/> muestra su nombre real.
    /// </summary>
    public string? Apodo         { get; set; }
}
