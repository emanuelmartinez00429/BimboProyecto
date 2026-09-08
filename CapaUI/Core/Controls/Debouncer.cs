using System;
using System.Threading;
using System.Threading.Tasks;

namespace CapaUI.Core.Controls;

/// <summary>
/// Espera un tiempo antes de ejecutar una acción y cancela la anterior si entra
/// otra llamada durante esa espera. Es la mecánica de "no dispares una consulta
/// por tecla", aislada de para qué se use.
/// </summary>
/// <remarks>
/// <para>
/// Existía copiada en cuatro lugares —<see cref="SuggestionDebouncer"/>,
/// <c>SelectorCatalogoModal</c>, <c>NotificacionesViewModel</c> y
/// <c>GhostTextBox</c>— y en tres de los cuatro el <see cref="CancellationTokenSource"/>
/// anterior quedaba sin disponer. Registrado como P-054.
/// </para>
/// <para>
/// La implementación correcta era la de <c>GhostTextBox.CancelGhostDebounce</c>;
/// esta clase es esa, extraída. <c>GhostTextBox</c> quedó sin migrar a propósito:
/// su cancelación se invoca desde varios puntos con semántica propia y cambiarla
/// era riesgo sin ganancia.
/// </para>
/// <para>
/// Es seguro llamarlo desde cualquier hilo: el reemplazo del token va por
/// <see cref="Interlocked.Exchange{T}(ref T, T)"/> porque
/// <c>NotificacionesViewModel</c> lo dispara desde el hilo de Realtime, no del UI.
/// </para>
/// </remarks>
public sealed class Debouncer : IDisposable
{
    private readonly int _ms;
    private CancellationTokenSource? _cts;
    private volatile bool _dispuesto;

    /// <param name="milisegundos">Tiempo de espera antes de ejecutar la acción.</param>
    public Debouncer(int milisegundos) => _ms = milisegundos;

    /// <summary>
    /// Cancela la ejecución pendiente, si hay alguna. Obligatorio cuando la
    /// espera dejó de tener sentido — por ejemplo al elegir una sugerencia: si
    /// no, la búsqueda en curso termina después de la selección y reabre el
    /// popup con la caja ya vacía.
    /// </summary>
    public void Cancelar() => Reemplazar(null);

    /// <summary>
    /// Espera el debounce y ejecuta <paramref name="accion"/>. Si durante la
    /// espera entra otra llamada, esta se cancela y la acción nunca corre — así
    /// que <paramref name="accion"/> puede escribir en la UI sin más guardas.
    /// </summary>
    public async Task EjecutarAsync(Func<CancellationToken, Task> accion)
    {
        if (_dispuesto) return;

        var nuevo = new CancellationTokenSource();
        Reemplazar(nuevo);
        var token = nuevo.Token;

        try
        {
            await Task.Delay(_ms, token);
            if (token.IsCancellationRequested || _dispuesto) return;

            await accion(token);
        }
        // Cancelación intencional (entró otra llamada): no es un error.
        catch (OperationCanceledException) { }
    }

    /// <summary>Variante síncrona, para acciones que no son async.</summary>
    public Task EjecutarAsync(Action accion) =>
        EjecutarAsync(_ => { accion(); return Task.CompletedTask; });

    // Cancel() antes de Dispose() es el orden que exige la documentación de
    // CancellationTokenSource: Cancel corre las registraciones —lo que completa
    // el Task.Delay en vuelo como cancelado— y recién entonces se puede liberar.
    private void Reemplazar(CancellationTokenSource? nuevo)
    {
        var previo = Interlocked.Exchange(ref _cts, nuevo);
        if (previo is null) return;
        previo.Cancel();
        previo.Dispose();
    }

    public void Dispose()
    {
        _dispuesto = true;
        Cancelar();
    }
}
