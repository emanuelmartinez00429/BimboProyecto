namespace CapaAplicacion.Presentaciones.Queries;

public enum OrdenPresentacion { IdAsc, NombreAsc, NombreDesc }

public class PresentacionFiltros
{
    public int? IdEstado { get; init; }

    public OrdenPresentacion Orden { get; init; } = OrdenPresentacion.IdAsc;
}
