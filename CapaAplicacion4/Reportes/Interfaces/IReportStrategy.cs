using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;
using CapaDominio.Reportes;

namespace CapaAplicacion.Reportes.Interfaces;

public interface IReportStrategy
{
    ReportFormat SupportedFormat { get; }
    Task<Result<byte[]>> GenerateAsync(TabularReportDto report, CancellationToken ct = default);
}
