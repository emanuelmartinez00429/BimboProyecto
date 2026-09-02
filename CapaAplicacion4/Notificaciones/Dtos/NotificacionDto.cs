namespace CapaAplicacion.Notificaciones.Dtos;

public sealed class NotificacionDto
{
    public long IdNotificacion { get; init; }
    public string CodigoTipo { get; init; } = string.Empty;
    public string NombreTipo { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;
    public string Severidad { get; init; } = "informativa";
    public string EstadoNotificacion { get; init; } = "activa";
    public DateTime FechaCreacion { get; init; }
    public DateTime? FechaLeida { get; init; }
    public DateTime? FechaArchivada { get; init; }
    public string Actor { get; init; } = string.Empty;
    public int? IdModulo { get; init; }
    public int? IdAccion { get; init; }
    public string? TablaOrigen { get; init; }
    public int? IdRegistroOrigen { get; init; }
    public string? MetadataJson { get; init; }

    public bool EstaLeida => FechaLeida.HasValue;
    public bool EstaArchivada => FechaArchivada.HasValue;
}

public sealed class FiltroNotificacionesDto
{
    public string EstadoBandeja { get; init; } = "todas";
    public string? Severidad { get; init; }
    public string? CodigoTipo { get; init; }
    public int Limite { get; init; } = 25;
    public DateTime? CursorFecha { get; init; }
    public long? CursorId { get; init; }
}
