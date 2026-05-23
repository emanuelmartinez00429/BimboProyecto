using System.Diagnostics;
using CapaAplicacion.Productos.Dtos;

namespace CapaUI.Services.Picker;

/// <summary>
/// Implementación de IPickerService.
/// Cada método abrirá su modal de búsqueda correspondiente cuando el módulo esté construido.
///
/// Patrón de implementación (cuando se construya un picker):
///   1. Crear un UserControl XxxPicker con buscador + lista
///   2. Mostrarlo como overlay modal sobre la ventana activa
///   3. Usar TaskCompletionSource&lt;XxxDto?&gt; para esperar la selección del usuario
///   4. Retornar el resultado (null si cancela)
/// </summary>
public class PickerService : IPickerService
{
    public Task<ProductoDto?> PickProductoAsync(CancellationToken ct = default)
    {
        // TODO: mostrar ProductoPicker modal (UserControl con buscador + DataGrid)
        // Se implementa cuando el formulario que lo requiera esté en construcción.
        Debug.WriteLine("[PickerService] PickProductoAsync: pendiente de implementación.");
        return Task.FromResult<ProductoDto?>(null);
    }
}
