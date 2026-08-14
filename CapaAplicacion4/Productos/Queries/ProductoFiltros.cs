namespace CapaAplicacion.Productos.Queries;

/// <summary>Orden del listado de productos.</summary>
public enum OrdenProducto
{
    /// <summary>Orden natural por id (el histórico).</summary>
    IdAsc,
    NombreAsc,
    NombreDesc,
}

public class ProductoFiltros
{
    public int? IdEstado     { get; init; }
    public int? IdFabricante { get; init; }
    public int? IdPais       { get; init; }
    public int? IdCategoria  { get; init; }

    /// <summary>
    /// Proveedor. productos no tiene id_proveedor: el filtro se resuelve a
    /// través de fabricante.
    /// </summary>
    public int? IdProveedor { get; init; }

    public OrdenProducto Orden { get; init; } = OrdenProducto.IdAsc;
}
