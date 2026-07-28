namespace CapaAplicacion.Presentaciones.Dtos;

public class PresentacionDto
{
    public int    Id          { get; init; }
    public string Nombre      { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public int    IdEstado    { get; init; }
}
