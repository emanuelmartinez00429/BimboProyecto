using System.Text.Json;

namespace CapaUI.Core.Seguridad;

/// <summary>
/// Conserva el identificador de una mutación mientras se reintenta con los
/// mismos parámetros. No es una cola ni una caché: solo protege un reintento
/// iniciado por el usuario en la misma pantalla.
/// </summary>
public sealed class SolicitudIdempotente
{
    private Guid? _idSolicitud;
    private string? _huella;

    public Guid Obtener(string operacion, object parametros)
    {
        var huella = $"{operacion}:{JsonSerializer.Serialize(parametros)}";
        if (_idSolicitud is null || !string.Equals(_huella, huella, StringComparison.Ordinal))
        {
            _idSolicitud = Guid.NewGuid();
            _huella = huella;
        }

        return _idSolicitud.Value;
    }

    public void Confirmar()
    {
        _idSolicitud = null;
        _huella = null;
    }
}
