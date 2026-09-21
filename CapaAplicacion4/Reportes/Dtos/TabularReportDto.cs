namespace CapaAplicacion.Reportes.Dtos;

public sealed class TabularReportDto
{
    public string Title { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public ReportAuthorDto Author { get; init; } = new();
    public string SheetName { get; init; } = "Reporte";
    /// <summary>
    /// Cantidad semántica de registros representados. Si se omite, se usa el
    /// número de filas, que conserva el comportamiento de los reportes actuales.
    /// </summary>
    public int? RecordCount { get; init; }
    public bool Landscape { get; init; } = true;
    public ReportBrandingDto Branding { get; init; } = new();
    public IReadOnlyList<ReportColumnDto> Columns { get; init; } = Array.Empty<ReportColumnDto>();
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = Array.Empty<IReadOnlyList<object?>>();
    public IReadOnlyList<ReportMetadataDto> Filters { get; init; } = Array.Empty<ReportMetadataDto>();
    public IReadOnlyList<ReportTotalDto> Totals { get; init; } = Array.Empty<ReportTotalDto>();
    public IReadOnlyList<ReportMetadataDto> FooterMetadata { get; init; } = Array.Empty<ReportMetadataDto>();
}

public sealed record ReportColumnDto(string Header, string? NumberFormat = null, double? WidthCm = null)
{
    public static implicit operator ReportColumnDto(string header) => new(header);
}
public sealed record ReportMetadataDto(string Label, string Value);
public sealed record ReportTotalDto(string Label, object? Value, string? NumberFormat = null);
public sealed class ReportBrandingDto
{
    public string CompanyName { get; init; } = "Bimbo Honduras";
    public byte[]? LogoBytes { get; init; }
}
