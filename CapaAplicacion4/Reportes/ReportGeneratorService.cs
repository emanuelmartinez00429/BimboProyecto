using CapaAplicacion.Common;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaDominio.Reportes;

namespace CapaAplicacion.Reportes;

public sealed class ReportGeneratorService : IReportGeneratorService
{
    private readonly IReadOnlyDictionary<ReportFormat, IReportStrategy> _strategies;

    public ReportGeneratorService(IEnumerable<IReportStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.SupportedFormat);
    }

    public Task<Result<byte[]>> GenerateAsync(
        TabularReportDto report,
        ReportFormat format,
        CancellationToken ct = default)
    {
        if (!_strategies.TryGetValue(format, out var strategy))
            return Task.FromResult(Result<byte[]>.Fail($"No hay un generador registrado para {format}."));

        return strategy.GenerateAsync(report, ct);
    }
}
