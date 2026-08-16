using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;
using CapaDominio.Reportes;

namespace CapaAplicacion.Reportes.Interfaces;

public interface IReportGeneratorService
{
    Task<Result<byte[]>> GenerateAsync(
        TabularReportDto report,
        ReportFormat format,
        CancellationToken ct = default);
}
