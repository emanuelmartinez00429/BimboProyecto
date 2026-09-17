using CapaAplicacion.Common;

namespace CapaAplicacion.Dashboard.Interfaces;

/// <summary>
/// Contrato de datos para el Dashboard de reportería y métricas operativas.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>
    /// Obtiene las métricas de conteo del catálogo (productos, proveedores, fabricantes).
    /// Sujeto a caché L1 de 5 minutos con tags TagsCache.CatalogosRaiz (ADR-026).
    /// </summary>
    Task<Result<KpisInventarioDto>> ObtenerKpisInventarioAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los KPIs de pesajes del período seleccionado versus el período anterior.
    /// Ejecución en una sola pasada contra consultar_kpis_pesajes (Zero-Cache).
    /// </summary>
    Task<Result<KpisPesajesDto>> ObtenerKpisPesajesAsync(PeriodoDashboard periodo, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los productos con mayor merma en el período indicado (Zero-Cache).
    /// </summary>
    Task<Result<IReadOnlyList<MermaProductoDto>>> ObtenerTopMermaAsync(PeriodoDashboard periodo, int top = 5, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los pesajes más recientes para el feed de la vista (Zero-Cache).
    /// </summary>
    Task<Result<IReadOnlyList<UltimoPesajeDto>>> ObtenerUltimosPesajesAsync(int cantidad = 5, CancellationToken ct = default);
}
