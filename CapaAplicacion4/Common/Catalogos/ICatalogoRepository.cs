using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Common.Catalogos;

/// <summary>
/// Lectura uniforme de los catálogos chicos del sistema (presentación, tara,
/// categoría, unidad, país, fabricante, proveedor).
///
/// Todos los métodos comparten la MISMA firma paginada a propósito: es lo que
/// permite que el selector genérico reciba un simple delegado y decida solo su
/// estrategia (todo en memoria vs. paginado server-side) mirando
/// <see cref="PagedResult{T}.Total"/> de la primera respuesta.
///
/// Las tablas no comparten esquema — cada implementación resuelve cuál es su
/// columna de etiqueta, cuál la de descripción y si tiene o no columna de estado.
/// </summary>
public interface ICatalogoRepository
{
    Task<Result<PagedResult<FiltroItem>>> GetPresentacionesAsync(
        string termino, int page, int size, CancellationToken ct = default);

    Task<Result<PagedResult<FiltroItem>>> GetTarasAsync(
        string termino, int page, int size, CancellationToken ct = default);

    Task<Result<PagedResult<FiltroItem>>> GetCategoriasAsync(
        string termino, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// Unidades de medida, opcionalmente acotadas a una categoría (masa, volumen,
    /// conteo — <c>tipo_unidad</c>). Mismo patrón de acotamiento que
    /// <see cref="GetFabricantesAsync"/> con <c>idProveedor</c>.
    /// </summary>
    Task<Result<PagedResult<FiltroItem>>> GetUnidadesAsync(
        string termino, int page, int size, int? idTipoUnidad = null, CancellationToken ct = default);

    Task<Result<PagedResult<FiltroItem>>> GetPaisesAsync(
        string termino, int page, int size, CancellationToken ct = default);

    Task<Result<PagedResult<FiltroItem>>> GetProveedoresAsync(
        string termino, int page, int size, CancellationToken ct = default);

    Task<Result<PagedResult<FiltroItem>>> GetProductosAsync(
        string termino, int page, int size, CancellationToken ct = default);

    /// <summary>
    /// Fabricantes, opcionalmente acotados a un proveedor (catálogo encadenado).
    /// El parámetro extra queda fuera de la firma uniforme capturándolo en el
    /// closure de la factory de configuración — ver <c>Catalogos.Fabricantes</c>.
    /// </summary>
    Task<Result<PagedResult<FiltroItem>>> GetFabricantesAsync(
        string termino, int page, int size, int? idProveedor = null, CancellationToken ct = default);
}
