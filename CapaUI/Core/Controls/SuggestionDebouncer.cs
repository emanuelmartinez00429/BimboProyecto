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
/// <para>
/// El debounce en sí vive en <see cref="Debouncer"/> desde P-054: esta clase
/// tenía su propia copia y era una de las tres que dejaban sin disponer el
/// <c>CancellationTokenSource</c> anterior. Lo que queda acá es lo propio del
/// buscador — el <c>Trim()</c>, el mínimo de 2 caracteres y el mapeo a
/// <see cref="SuggestionItemData"/>.
/// </para>
/// </remarks>
public sealed class SuggestionDebouncer : IDisposable
{
    /// <summary>Tiempo de espera antes de disparar la búsqueda, en ms.</summary>
    public const int DebounceMs = 200;

    private readonly Debouncer _debouncer = new(DebounceMs);

    /// <summary>
    /// Cancela la búsqueda en vuelo, si hay alguna.
    /// Obligatorio al seleccionar una sugerencia: si no, la búsqueda en curso
    /// termina después de la selección y reabre el popup con la caja ya vacía.
    /// </summary>
    public void Cancelar() => _debouncer.Cancelar();

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
    public Task EjecutarAsync(
        string query,
        Func<string, CancellationToken, Task<IReadOnlyList<SuggestionItemData>?>> buscar,
        Action<IReadOnlyList<SuggestionItemData>?> aplicar)
    {
        var q = query.Trim();

        // Menos de 2 caracteres no se busca: cierra el popup y evita peticiones
        // innecesarias de una sola letra a la base de datos mientras se teclea.
        // Igual hay que cancelar lo que estuviera en vuelo — si no, un borrado
        // rápido deja llegar la búsqueda del texto anterior sobre la caja vacía.
        if (q.Length < 2)
        {
            _debouncer.Cancelar();
            aplicar(null);
            return Task.CompletedTask;
        }

        return _debouncer.EjecutarAsync(async token =>
        {
            var items = await buscar(q, token);
            if (token.IsCancellationRequested) return;

            aplicar(items);
        });
    }

    public void Dispose() => _debouncer.Dispose();
}
