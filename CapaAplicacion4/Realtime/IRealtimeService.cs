namespace CapaAplicacion.Realtime;

/// <summary>
/// Facade sobre Supabase Realtime. Gestiona suscripciones por tabla
/// con lifecycle automático de canales y Dispatcher marshaling.
/// </summary>
public interface IRealtimeService
{
    /// <summary>
    /// Suscribe un handler a cambios en la tabla especificada.
    /// Si es el primer suscriptor, abre el canal de Supabase.
    /// El handler se invoca siempre en el UI thread.
    /// </summary>
    Task SuscribirAsync(string tabla, Action<CambioRealtime> handler);

    /// <summary>
    /// Desuscribe un handler. Si era el último suscriptor,
    /// cierra el canal de Supabase automáticamente.
    /// </summary>
    void Desuscribir(string tabla, Action<CambioRealtime> handler);

    /// <summary>
    /// Cierra todos los canales y desconecta el WebSocket.
    /// Llamar al cerrar sesión para liberar recursos.
    /// </summary>
    Task DesconectarAsync();
}
