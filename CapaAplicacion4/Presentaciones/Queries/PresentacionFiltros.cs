namespace CapaAplicacion.Presentaciones.Queries;

/// <summary>
/// Filtros para el listado de presentaciones de producto.
/// IdEstado sigue la convención EstadoRegistro: 1 = activo, 2 = inactivo, null = todos.
/// </summary>
public class PresentacionFiltros
{
    public int? IdEstado { get; init; }
}
