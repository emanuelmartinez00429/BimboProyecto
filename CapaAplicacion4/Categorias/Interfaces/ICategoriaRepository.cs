using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Queries;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Categorias.Interfaces;

public interface ICategoriaRepository
{
    Task<Result<PagedResult<CategoriaDto>>>   GetPagedAsync(int page, int size, CategoriaFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CategoriaDto>>> BuscarSugerenciasAsync(string termino, CategoriaFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                          GetPaginaDeRegistroAsync(int id, int size, CategoriaFiltros filtros, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(CategoriaDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(CategoriaDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,           CancellationToken ct = default);
}
