using CapaAplicacion.Realtime;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CapaUI.Core.MVVM;

/// <summary>
/// Base para ViewModels que reaccionan a Supabase Realtime.
///
/// Uso:
///   1. Heredar de RealtimeAwareViewModel en lugar de ObservableObject.
///   2. Inyectar IRealtimeService en el constructor y pasarlo a base(realtime).
///   3. Llamar Observar("tabla", handler) en CargarDatosAsync() o el constructor.
///      La baja se hace automáticamente al Dispose() — no se puede olvidar.
///   4. Sobrescribir OnDispose() para liberar recursos adicionales (CTS, timers, etc.).
///
/// Patrón: Template Method — Dispose() llama OnDispose() para extensión controlada.
/// </summary>
public abstract partial class RealtimeAwareViewModel : ObservableObject, IDisposable
{
    private readonly List<IDisposable> _suscripciones = new();
    private readonly object _gate = new();

    protected IRealtimeService Realtime { get; }
    protected bool Disposed { get; private set; }

    protected RealtimeAwareViewModel(IRealtimeService realtime) => Realtime = realtime;

    /// <summary>
    /// Suscribe a una tabla y registra el token de baja.
    /// La desuscripción ocurre automáticamente en Dispose().
    /// Si el VM ya fue dispuesto, la llamada es ignorada.
    /// </summary>
    protected void Observar(string tabla, Action<CambioRealtime> handler)
    {
        lock (_gate)
        {
            if (Disposed) return;
            _suscripciones.Add(Realtime.Observar(tabla, handler));
        }
    }

    public void Dispose()
    {
        List<IDisposable> aLiberar;
        lock (_gate)
        {
            if (Disposed) return;
            Disposed = true;
            aLiberar = new List<IDisposable>(_suscripciones);
            _suscripciones.Clear();
        }

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
