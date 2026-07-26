namespace CapaAplicacion.Bitacora.Dtos;

public sealed class BitacoraDto
{
    public int       IdBitacora         { get; init; }
    public DateTime? FechaHora          { get; init; }
    public string    AliasUsuario       { get; init; } = string.Empty;
    public string    NombreModulo       { get; init; } = string.Empty;
    public string    NombreAccion       { get; init; } = string.Empty;
    public string    CampoAfectado      { get; init; } = string.Empty;
    public string    EstadoAnterior     { get; init; } = string.Empty;
    public string    EstadoActual       { get; init; } = string.Empty;
    public string?   CampoExtra         { get; init; }
    public string?   TablaAfectada      { get; init; }
    public int?      IdRegistroAfectado { get; init; }
}
