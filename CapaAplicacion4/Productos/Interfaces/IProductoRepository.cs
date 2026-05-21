using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Productos.Interfaces;

public interface IProductoRepository
{
    // Lectura
    Task<PagedResult<ProductoDto>>   GetPagedAsync(int page, int size, ProductoFiltros filtros, CancellationToken ct = default);
    Task<IReadOnlyList<ProductoDto>> BuscarSugerenciasAsync(string termino, ProductoFiltros filtros, CancellationToken ct = default);
    Task<IReadOnlyList<FiltroItem>>  GetFabricantesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FiltroItem>>  GetPaisesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<FiltroItem>>  GetCategoriasAsync(CancellationToken ct = default);

    // Escritura
    Task<Result<int>> CreateAsync(ProductoDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(ProductoDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,          CancellationToken ct = default);
}
