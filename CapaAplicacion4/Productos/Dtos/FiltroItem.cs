namespace CapaAplicacion.Productos.Dtos;

public class FiltroItem
{
    public int?   Id     { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public override string ToString() => Nombre;
}
