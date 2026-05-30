namespace CapaAplicacion.Categorias.Queries;

/// <summary>
/// Filtros para el listado de categorías.
/// IdEstado sigue la convención EstadoRegistro: 1 = activo (estado_categoria = true),
/// 2 = inactivo (estado_categoria = false), null = todos.
/// El repositorio traduce de int a bool al filtrar en Supabase.
/// </summary>
public class CategoriaFiltros
{
    public int? IdEstado { get; init; }
}
