namespace CapaAplicacion.Productos.Queries;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items     { get; init; } = [];
    public int              Total     { get; init; }
    public int              Activos   { get; init; }
    public int              Inactivos { get; init; }
}
