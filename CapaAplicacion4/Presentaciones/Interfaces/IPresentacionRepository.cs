using CapaAplicacion.Common;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Queries;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Presentaciones.Interfaces;

public interface IPresentacionRepository
{
    Task<Result<PagedResult<PresentacionDto>>>   GetPagedAsync(int page, int size, PresentacionFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PresentacionDto>>> BuscarSugerenciasAsync(string termino, PresentacionFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                            GetPaginaDeRegistroAsync(int id, int size, PresentacionFiltros filtros, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(PresentacionDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(PresentacionDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,              CancellationToken ct = default);
}
