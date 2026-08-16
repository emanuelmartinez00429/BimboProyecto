namespace CapaAplicacion.Reportes.Dtos;

public sealed class TabularReportDto
{
    public string Title { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public ReportAuthorDto Author { get; init; } = new();
    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();
}
