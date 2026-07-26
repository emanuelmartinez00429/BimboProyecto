namespace CapaAplicacion.Bitacora.Queries;

/// <summary>
/// Filtros para el listado de bitácora. FechaDesde/FechaHasta filtran sobre
/// fecha_hora (timestamptz) — el repositorio se encarga de llevar FechaHasta
/// al final del día seleccionado (23:59:59) para no perder registros de esa fecha.
/// </summary>
public class BitacoraFiltros
{
    public int?      IdUsuario  { get; init; }
    public int?      IdModulo   { get; init; }
    public int?      IdAccion   { get; init; }
    public DateTime? FechaDesde { get; init; }
    public DateTime? FechaHasta { get; init; }
}
