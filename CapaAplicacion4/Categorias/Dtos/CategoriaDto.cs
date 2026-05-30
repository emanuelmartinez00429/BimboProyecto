namespace CapaAplicacion.Categorias.Dtos;

public class CategoriaDto
{
    public int    Id              { get; init; }
    public string Nombre          { get; init; } = string.Empty;
    public string Descripcion     { get; init; } = string.Empty;
    public bool   EstadoCategoria { get; init; }  // true = activo
}
