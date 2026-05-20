namespace CapaDominio.Entities;

public class Empleado
{
    public int    Id        { get; set; }
    public string Nombres   { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Identidad { get; set; } = string.Empty;
    public string Telefono  { get; set; } = string.Empty;
    public string Correo    { get; set; } = string.Empty;
    public int    IdEstado  { get; set; }
}
