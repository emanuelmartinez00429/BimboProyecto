using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;

namespace CapaAplicacion.Pesaje.Interfaces;

/// <summary>
/// Contrato de solo lectura para el selector de productos del módulo de Pesaje
/// (modal "Seleccionar producto"). Reutiliza <see cref="ProductoDto"/> y el
/// patrón Result — no modifica la arquitectura del Buscador Universal ni del
/// formulario de Productos; es una extensión (clase nueva) registrada en DI.
///
/// La relación producto→proveedor se resuelve por el puente existente
/// productos.id_fabricante → fabricante.id_proveedor (sin cambios de esquema).
/// </summary>
public interface IPickerProductoRepository
{
    /// <summary>Top N productos del proveedor (lista predefinida al abrir el modal).</summary>
    Task<Result<IReadOnlyList<ProductoDto>>> TopPorProveedorAsync(
        int idProveedor, int limit = 10, CancellationToken ct = default);

    /// <summary>Búsqueda (nombre o código, ILike) acotada a un proveedor.</summary>
    Task<Result<IReadOnlyList<ProductoDto>>> BuscarPorProveedorAsync(
        string termino, int idProveedor, CancellationToken ct = default);

    /// <summary>Búsqueda global en todo el catálogo ("Buscar en todos los proveedores").</summary>
    Task<Result<IReadOnlyList<ProductoDto>>> BuscarTodosAsync(
        string termino, CancellationToken ct = default);
}
