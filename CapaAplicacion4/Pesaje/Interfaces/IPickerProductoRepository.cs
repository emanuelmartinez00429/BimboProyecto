using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

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

    /// <summary>
    /// Página del catálogo para el selector con tabla (Fase 8). Paginación server-side.
    /// <para/>
    /// <paramref name="idProveedor"/> null = todo el catálogo; con valor acota por el
    /// puente producto→fabricante→proveedor.
    /// <paramref name="termino"/> vacío o null = sin filtro de texto (a diferencia de
    /// los métodos de búsqueda, que exigen término).
    /// </summary>
    Task<Result<PagedResult<ProductoDto>>> GetPagedAsync(
        int? idProveedor, string? termino, int page, int size, CancellationToken ct = default);
}
