namespace CapaAplicacion.Contactos.Fabricantes.Dtos;

public class ContactoFabricanteDto
{
    public int    Id           { get; init; }
    public int    IdFabricante { get; init; }
    public string Nombre       { get; init; } = string.Empty;
    public string Telefono     { get; init; } = string.Empty;
    public string Correo       { get; init; } = string.Empty;
    public int    IdEstado     { get; init; }
}
