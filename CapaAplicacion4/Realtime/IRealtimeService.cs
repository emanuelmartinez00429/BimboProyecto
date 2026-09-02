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
    /// Suscribe y devuelve un token IDisposable.
    /// Al hacer Dispose() del token la suscripción se cancela automáticamente.
    /// Patrón preferido para ViewModels — imposible olvidar la baja.
    /// </summary>
    IDisposable Observar(string tabla, Action<CambioRealtime> handler);

    /// <summary>
    /// Abre una suscripción filtrada y no retorna hasta que el canal fue suscrito.
    /// Se usa cuando el orden suscribir-antes-de-consultar es parte del contrato.
    /// </summary>
    Task<IDisposable> ObservarAsync(
        string tabla,
        string filtro,
        Action<CambioRealtime> handler,
        CancellationToken ct = default);

    /// <summary>
    /// Cierra todos los canales. El WebSocket se mantiene vivo
    /// para reutilizarse en el próximo login sin latencia de reconexión.
    /// Llamar al cerrar sesión para liberar recursos.
    /// </summary>
    Task DesconectarAsync();
}
