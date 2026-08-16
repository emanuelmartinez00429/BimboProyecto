using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;

namespace CapaAplicacion.Reportes.Interfaces;

public interface IReporteRepository
{
    Task<Result<int>> RegistrarAsync(ReporteRegistroDto reporte, CancellationToken ct = default);
}
