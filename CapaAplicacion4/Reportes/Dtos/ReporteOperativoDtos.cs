namespace CapaAplicacion.Reportes.Dtos;

public sealed record EntradaMateriaPrimaFiltro(int IdProducto, int IdProveedor, int IdUsuario);
public sealed record ProveedorReporteFiltro(int IdProveedor, DateTime FechaDesde, DateTime FechaHasta, int IdUsuario);
public sealed record MermasReporteFiltro(DateTime FechaDesde, DateTime FechaHasta, int? IdCategoria, int IdUsuario);

public sealed class EntradaMateriaPrimaFila
{
    public int IdPesaje { get; init; }
    public DateTime FechaHora { get; init; }
    public string Producto { get; init; } = string.Empty;
    public string Proveedor { get; init; } = string.Empty;
    public string Placa { get; init; } = string.Empty;
    public string Pesador { get; init; } = string.Empty;
    public decimal PesoBruto { get; init; }
    public decimal PesoTara { get; init; }
    public decimal PesoNeto { get; init; }
}

public sealed class ProveedorReporteFila
{
    public int IdMovimientoProducto { get; init; }
    public DateTime FechaIngreso { get; init; }
    public string Producto { get; init; } = string.Empty;
    public decimal? BultosEstimados { get; init; }
    public decimal PesoTeorico { get; init; }
    public decimal PesoRecibido { get; init; }
    public decimal DiferenciaKg { get; init; }
    public decimal DiferenciaMonetaria { get; init; }
}

public sealed class MermaReporteFila
{
    public int IdProducto { get; init; }
    public string Producto { get; init; } = string.Empty;
    public string Proveedor { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public int CantidadEntradas { get; init; }
    public decimal PesoTeorico { get; init; }
    public decimal PesoRecibido { get; init; }
    public decimal DiferenciaKg { get; init; }
    public decimal? MermaPorcentaje { get; init; }
}

public sealed class ProductoPruebaFila
{
    public int IdProducto { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Proveedor { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
}
