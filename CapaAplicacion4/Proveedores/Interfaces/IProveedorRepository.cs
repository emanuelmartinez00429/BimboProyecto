using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Queries;

namespace CapaAplicacion.Proveedores.Interfaces;

public interface IProveedorRepository
{
    Task<Result<PagedResult<ProveedorDto>>>   GetPagedAsync(int page, int size, ProveedorFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ProveedorDto>>> BuscarSugerenciasAsync(string termino, ProveedorFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                         GetPaginaDeRegistroAsync(int id, int size, ProveedorFiltros filtros, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ProveedorDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      UpdateAsync(ProveedorDto dto, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,           Guid idSolicitud, CancellationToken ct = default);
}
