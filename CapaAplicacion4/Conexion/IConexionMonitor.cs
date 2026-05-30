namespace CapaAplicacion.Conexion;

/// <summary>
/// Monitor de conectividad. Vigila la red de forma continua mientras hay sesión
/// y publica el <see cref="EstadoConexion"/> actual.
///
/// Patrón espejo de <c>IRealtimeService</c>: contrato en CapaAplicacion,
/// implementación singleton en CapaDatos. Los eventos se despachan en el UI thread.
/// </summary>
public interface IConexionMonitor
{
    /// <summary>Último estado publicado.</summary>
    EstadoConexion Estado { get; }

    /// <summary>
    /// Se dispara cuando el estado publicado cambia (ya con histéresis aplicada).
    /// Siempre en el UI thread.
    /// </summary>
    event EventHandler<EstadoConexion>? EstadoCambiado;

    /// <summary>
    /// Se dispara al recuperar la conexión (transición Degradado|SinConexion → Conectado).
    /// Útil para refrescar la vista activa y cerrar el hueco de eventos Realtime perdidos.
    /// Siempre en el UI thread.
    /// </summary>
    event EventHandler? Reconectado;

    /// <summary>
    /// Empieza a vigilar la red: suscribe a cambios de NIC y arranca el sondeo periódico.
    /// Idempotente. Llamar tras el login (al mostrar el shell).
    /// </summary>
    void Iniciar();

    /// <summary>
    /// Detiene la vigilancia: desuscribe los eventos de red y para el sondeo.
    /// Idempotente. Llamar al cerrar sesión.
    /// </summary>
    void Detener();
}
