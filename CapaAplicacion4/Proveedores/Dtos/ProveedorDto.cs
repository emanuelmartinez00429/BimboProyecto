namespace CapaAplicacion.Proveedores.Dtos;

public class ProveedorDto
{
    public int    Id         { get; init; }
    public string Nombre     { get; init; } = string.Empty;
    public string Rtn        { get; init; } = string.Empty;
    public string Telefono   { get; init; } = string.Empty;
    public string Correo     { get; init; } = string.Empty;
    public string Direccion  { get; init; } = string.Empty;
    public int    IdEstado   { get; init; }
}
