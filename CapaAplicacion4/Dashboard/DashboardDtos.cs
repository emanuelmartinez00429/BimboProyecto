namespace CapaAplicacion.Dashboard;

/// <summary>
/// Período de consolidación temporal para los indicadores del Dashboard.
/// </summary>
public enum PeriodoDashboard
{
    Hoy,
    Semana,
    Mes
}

/// <summary>
/// Métricas consolidadas del catálogo de inventario.
/// </summary>
public sealed record KpisInventarioDto(
    int TotalProductos,
    int TotalProveedores,
    int TotalMarcas,
    int? DeltaProductos = null,
    int? DeltaProveedores = null
);

/// <summary>
/// Métricas de pesajes y mermas calculadas para el período actual comparado con el período anterior.
/// </summary>
public sealed record KpisPesajesDto(
    int PesajesActual,
    int PesajesAnterior,
    double? DeltaPesajesPct,
    decimal NetoActual,
    decimal NetoAnterior,
    double? DeltaNetoPct,
    decimal TeoricoActual,
    decimal RecibidoActual,
    double? PctMermaActual,
    double? PctMermaAnterior,
    double? DeltaMermaPct
);

/// <summary>
/// Representa un producto en el ranking de mayor porcentaje de merma.
/// </summary>
public sealed record MermaProductoDto(
    string Nombre,
    double Porcentaje,
    int Kilos,
    int IdProducto = 0
);

/// <summary>
/// Representa un pesaje reciente para el feed en tiempo real.
/// </summary>
public sealed record UltimoPesajeDto(
    string Codigo,
    string Producto,
    string Kilos,
    string Hora,
    bool EsAlerta
);
