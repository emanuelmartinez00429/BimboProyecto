using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;

namespace CapaAplicacion.Reportes.Interfaces;

public interface IReporteConsultaRepository
{
    Task<Result<IReadOnlyList<EntradaMateriaPrimaFila>>> ConsultarEntradaMateriaPrimaAsync(EntradaMateriaPrimaFiltro filtro, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProveedorReporteFila>>> ConsultarProveedorAsync(ProveedorReporteFiltro filtro, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MermaReporteFila>>> ConsultarMermasAsync(MermasReporteFiltro filtro, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductoPruebaFila>>> ConsultarPrimerosProductosAsync(int idUsuario, CancellationToken ct = default);
}
