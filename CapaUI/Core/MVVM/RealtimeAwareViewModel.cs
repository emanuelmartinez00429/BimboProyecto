using CapaAplicacion.Conexion;
using CapaAplicacion.Realtime;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Core.MVVM;

/// <summary>
/// Base para ViewModels que reaccionan a Supabase Realtime y al monitor de conexión.
///
/// Uso:
///   1. Heredar de RealtimeAwareViewModel en lugar de ObservableObject.
///   2. Inyectar IRealtimeService + IConexionMonitor y pasarlos a base(realtime, conexionMonitor).
///   3. Llamar Observar("tabla", handler) en CargarDatosAsync() o el constructor.
///      La baja se hace automáticamente al Dispose() — no se puede olvidar.
///   4. (Opcional) Sobrescribir OnReconexionAsync() para recargar datos al volver la conexión.
///   5. (Opcional) Sobrescribir OnDispose() para liberar recursos adicionales (CTS, timers, etc.).
///
/// Patrón: Template Method — Dispose() llama OnDispose(); Reconectado llama OnReconexionAsync().
/// </summary>
public abstract partial class RealtimeAwareViewModel : ObservableObject, IDisposable
{
    private readonly List<IDisposable> _suscripciones    = new();
    private readonly HashSet<string>   _tablasObservadas = new();
    private readonly object            _gate             = new();

    protected IRealtimeService Realtime { get; }
    private readonly IConexionMonitor _conexionMonitor;
    protected bool Disposed { get; private set; }

    protected RealtimeAwareViewModel(IRealtimeService realtime, IConexionMonitor conexionMonitor)
    {
        Realtime         = realtime;
        _conexionMonitor = conexionMonitor;
        _conexionMonitor.Reconectado += OnReconectadoInterno;
    }

    /// <summary>
    /// Suscribe a una tabla y registra el token de baja. Idempotente por tabla: re-suscribir
    /// la misma tabla es no-op, de modo que recargar tras una reconexión no duplique handlers
    /// de Realtime. La desuscripción ocurre automáticamente en Dispose().
    /// Si el VM ya fue dispuesto, la llamada es ignorada.
    /// </summary>
    protected void Observar(string tabla, Action<CambioRealtime> handler)
    {
        lock (_gate)
        {
            if (Disposed) return;
            if (!_tablasObservadas.Add(tabla)) return;   // ya suscrita → no duplicar
            _suscripciones.Add(Realtime.Observar(tabla, handler));
        }
    }

    // ── Reconexión ────────────────────────────────────────────────────

    // El evento llega ya en el UI thread (el monitor marshala con SynchronizationContext).
    private void OnReconectadoInterno(object? sender, EventArgs e)
    {
        if (Disposed) return;
        _ = OnReconexionAsync();
    }

    /// <summary>
    /// Se invoca al recuperar la conexión. Por defecto no hace nada; los VMs con datos del
    /// servidor lo sobrescriben para recargar y cerrar el hueco de eventos Realtime perdidos
    /// durante la caída. Como Observar() es idempotente, recargar (incluso re-llamando a la
    /// carga inicial) es seguro y no duplica suscripciones.
    /// </summary>
    protected virtual Task OnReconexionAsync() => Task.CompletedTask;

    public void Dispose()
    {
        List<IDisposable> aLiberar;
        lock (_gate)
        {
            if (Disposed) return;
            Disposed = true;
            aLiberar = new List<IDisposable>(_suscripciones);
            _suscripciones.Clear();
            _tablasObservadas.Clear();
        }

        // Baja del monitor (singleton) — imprescindible para no retener este VM
        _conexionMonitor.Reconectado -= OnReconectadoInterno;

        foreach (var s in aLiberar)
            s.Dispose();

        OnDispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Extensión para que el VM concreto libere sus propios recursos
    /// (CancellationTokenSource, DispatcherTimer, etc.).
    /// Se llama después de liberar todas las suscripciones Realtime.
    /// </summary>
    protected virtual void OnDispose() { }
}
