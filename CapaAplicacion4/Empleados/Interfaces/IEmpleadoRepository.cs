using CapaAplicacion.Common;
using CapaAplicacion.Empleados.Dtos;
using CapaAplicacion.Empleados.Queries;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Empleados.Interfaces;

/// <summary>
/// Repositorio de lectura del formulario de Empleados. Solo consulta —
/// la escritura (crear/editar) todavía no está habilitada en la UI,
/// así que este contrato no expone Create/Update/Delete todavía.
/// </summary>
public interface IEmpleadoRepository
{
    Task<Result<PagedResult<EmpleadoDto>>>   GetPagedAsync(int page, int size, EmpleadoFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EmpleadoDto>>> BuscarSugerenciasAsync(string termino, EmpleadoFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                        GetPaginaDeRegistroAsync(int id, int size, EmpleadoFiltros filtros, CancellationToken ct = default);
}
