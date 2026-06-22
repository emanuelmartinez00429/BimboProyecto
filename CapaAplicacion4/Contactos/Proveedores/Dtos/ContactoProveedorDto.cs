namespace CapaAplicacion.Contactos.Proveedores.Dtos;

public class ContactoProveedorDto
{
    public int    Id          { get; init; }
    public int    IdProveedor { get; init; }
    public string Nombre      { get; init; } = string.Empty;
    public string Telefono    { get; init; } = string.Empty;
    public string Correo      { get; init; } = string.Empty;
    public int    IdEstado    { get; init; }
}
