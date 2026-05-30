namespace CapaAplicacion.Fabricantes.Dtos;

public class FabricanteDto
{
    public int    Id              { get; init; }
    public string Nombre          { get; init; } = string.Empty;
    public string Descripcion     { get; init; } = string.Empty;
    public int?   IdProveedor     { get; init; }
    public string NombreProveedor { get; init; } = string.Empty;
    public int?   IdPais          { get; init; }
    public string NombrePais      { get; init; } = string.Empty;
    public int    IdEstado        { get; init; }
}
