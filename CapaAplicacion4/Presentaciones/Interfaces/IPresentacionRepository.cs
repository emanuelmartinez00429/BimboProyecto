using CapaAplicacion.Common;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Queries;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Presentaciones.Interfaces;

public interface IPresentacionRepository
{
    Task<Result<PagedResult<PresentacionDto>>>   GetPagedAsync(int page, int size, PresentacionFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PresentacionDto>>> BuscarSugerenciasAsync(string termino, PresentacionFiltros filtros, CancellationToken ct = default);

    /// <summary>
    /// Recibe el DTO y no solo el id porque con orden A-Z/Z-A activo hay que
    /// comparar por nombre, no por PK. Mismo criterio que
    /// ProductoCrudRepository.GetPaginaDeProductoAsync.
    /// </summary>
    Task<Result<int>> GetPaginaDeRegistroAsync(PresentacionDto dto, int size, PresentacionFiltros filtros, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(PresentacionDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(PresentacionDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,              CancellationToken ct = default);
}
