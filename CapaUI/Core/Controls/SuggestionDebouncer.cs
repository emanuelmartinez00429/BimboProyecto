using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CapaUI.Core.Controls;

/// <summary>
/// Debounce + cancelación para el buscador con sugerencias del
/// <see cref="SuggestionSearchBox"/>.
/// </summary>
/// <remarks>
/// <para>
/// Se usa por <b>composición</b>, no por herencia: los ViewModels de pantalla ya
/// heredan de <c>RealtimeAwareViewModel</c> u <c>ObservableObject</c> según el
/// módulo, así que una clase base común no era una opción.
/// </para>
/// <para>
/// Antes esta mecánica estaba copiada literal en los 9 ViewModels que usan el
/// control — ~28 líneas idénticas de las que variaba <b>una sola</b>: la llamada
/// al repositorio. Esa duplicación es la que produjo dos defectos replicados
/// (2026-07-26 y 2026-07-28) y estaba registrada como P-026.
/// </para>
/// </remarks>
public sealed class SuggestionDebouncer : IDisposable
{
    /// <summary>Tiempo de espera antes de disparar la búsqueda, en ms.</summary>
    public const int DebounceMs = 200;

    private CancellationTokenSource? _cts;

    /// <summary>
    /// Cancela la búsqueda en vuelo, si hay alguna.
    /// Obligatorio al seleccionar una sugerencia: si no, la búsqueda en curso
    /// termina después de la selección y reabre el popup con la caja ya vacía.
    /// </summary>
    public void Cancelar() => _cts?.Cancel();

    /// <summary>
    /// Espera el debounce y ejecuta <paramref name="buscar"/>. Si durante la
    /// espera entra otra pulsación, la anterior se cancela y nunca aplica.
    /// </summary>
    /// <param name="query">Texto crudo del buscador; se le hace <c>Trim()</c>.</param>
    /// <param name="buscar">
    /// Búsqueda contra el repositorio. Devuelve <c>null</c> si falló — el
    /// llamador la trata igual que "sin resultados".
    /// </param>
    /// <param name="aplicar">
    /// Recibe las sugerencias mapeadas, o <c>null</c> para cerrar el popup.
    /// Solo se invoca si la búsqueda no fue cancelada, así que puede escribir
    /// en la UI sin más guardas.
    /// </param>
    public async Task EjecutarAsync(
        string query,
        Func<string, CancellationToken, Task<IReadOnlyList<SuggestionItemData>?>> buscar,
        Action<IReadOnlyList<SuggestionItemData>?> aplicar)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var q = query.Trim();
        if (string.IsNullOrEmpty(q)) { aplicar(null); return; }

        try
        {
            await Task.Delay(DebounceMs, token);
            if (token.IsCancellationRequested) return;

            var items = await buscar(q, token);
            if (token.IsCancellationRequested) return;

            aplicar(items);
        }
        // Cancelación intencional (el usuario siguió escribiendo): no es un error.
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
