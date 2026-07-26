namespace CapaAplicacion.Empleados.Queries;

/// <summary>
/// Filtros para el listado de empleados. IdEstado sigue la convención
/// EstadoRegistro: 1 = activo, 2 = inactivo, null = todos.
/// </summary>
public class EmpleadoFiltros
{
    public int? IdEstado { get; init; }
}
