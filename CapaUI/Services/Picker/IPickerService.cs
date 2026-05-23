using CapaAplicacion.Productos.Dtos;

namespace CapaUI.Services.Picker;

/// <summary>
/// Abre un modal de búsqueda/selección y retorna el registro elegido,
/// o null si el usuario cancela. Se usa desde formularios de edición
/// que necesitan relacionar entidades de otros módulos.
/// </summary>
public interface IPickerService
{
    /// <summary>Abre el picker de Productos y retorna el seleccionado.</summary>
    Task<ProductoDto?> PickProductoAsync(CancellationToken ct = default);

    // ── Pickers a agregar cuando cada módulo esté construido ─────────────
    // Task<EmpleadoDto?>  PickEmpleadoAsync(CancellationToken ct = default);
    // Task<ProveedorDto?> PickProveedorAsync(CancellationToken ct = default);
    // Task<ClienteDto?>   PickClienteAsync(CancellationToken ct = default);
}
