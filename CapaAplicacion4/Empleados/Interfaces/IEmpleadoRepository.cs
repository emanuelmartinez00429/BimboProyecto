using CapaAplicacion.Common;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Queries;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Empleados.Interfaces;

/// <summary>
/// Repositorio CRUD del formulario de Empleados.
/// </summary>
public interface IEmpleadoRepository
{
    Task<Result<PagedResult<EmpleadoDto>>>   GetPagedAsync(int page, int size, EmpleadoFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmpleadoDto>>> BuscarSugerenciasAsync(string termino, EmpleadoFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                        GetPaginaDeRegistroAsync(int id, int size, EmpleadoFiltros filtros, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(EmpleadoDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(EmpleadoDto dto, CancellationToken ct = default);
    Task<Result>      CambiarEstadoAsync(int id, int nuevoEstado, CancellationToken ct = default);
}
