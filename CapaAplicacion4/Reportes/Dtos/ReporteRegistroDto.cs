namespace CapaAplicacion.Reportes.Dtos;

public sealed class ReporteRegistroDto
{
    public string NombreReporte { get; init; } = string.Empty;
    public string TipoReporte { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public DateTime? FechaDesde { get; init; }
    public DateTime? FechaHasta { get; init; }
    public string ParametrosJson { get; init; } = "{}";
    public int UsuarioIngresando { get; init; }
}
