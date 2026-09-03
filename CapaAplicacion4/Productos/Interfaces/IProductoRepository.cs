using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Productos.Interfaces;

public interface IProductoRepository
{
    // Lectura
    Task<Result<PagedResult<ProductoDto>>>   GetPagedAsync(int page, int size, ProductoFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProductoDto>>> BuscarSugerenciasAsync(string termino, ProductoFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetFabricantesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetPaisesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>>  GetCategoriasAsync(CancellationToken ct = default);
    /// <summary>
    /// Página en la que cae un producto con los filtros y el ORDEN actuales.
    /// Recibe el DTO completo (no solo el id) porque el cálculo depende de la
    /// columna por la que se está ordenando: con orden alfabético hay que
    /// comparar por nombre, y el llamador ya tiene ese dato a mano.
    /// </summary>
    Task<Result<int>>                        GetPaginaDeProductoAsync(ProductoDto producto, int size, ProductoFiltros filtros, CancellationToken ct = default);

    // Escritura
    Task<Result<int>> CreateAsync(ProductoDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      UpdateAsync(ProductoDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,          Guid idSolicitud, CancellationToken ct = default);
}
