namespace CapaAplicacion.Conexion;

/// <summary>
/// Estado de conectividad publicado por <see cref="IConexionMonitor"/>.
/// </summary>
public enum EstadoConexion
{
    /// <summary>Aún no se ha hecho el primer sondeo (estado inicial).</summary>
    Desconocido,

    /// <summary>Hay interfaz de red y el ping llega en tiempo y forma.</summary>
    Conectado,

    /// <summary>Hay interfaz de red (cable/wifi) pero el ping no llega o tarda demasiado.</summary>
    Degradado,

    /// <summary>No hay ninguna interfaz de red operativa.</summary>
    SinConexion
}
