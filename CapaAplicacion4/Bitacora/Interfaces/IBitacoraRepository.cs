using CapaAplicacion.Bitacora.Dtos;
using CapaAplicacion.Bitacora.Queries;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Bitacora.Interfaces;

/// <summary>
/// Repositorio de bitácora (auditoría). Solo lectura — la bitácora la escribe
/// el propio sistema al ejecutar acciones, nunca esta pantalla.
/// </summary>
public interface IBitacoraRepository
{
    Task<Result<PagedResult<BitacoraDto>>>   GetPagedAsync(int page, int size, BitacoraFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BitacoraDto>>> BuscarSugerenciasAsync(string termino, BitacoraFiltros filtros, CancellationToken ct = default);
    Task<Result<int>>                        GetPaginaDeRegistroAsync(int id, int size, BitacoraFiltros filtros, CancellationToken ct = default);

    // Lookups para poblar los dropdowns de filtro de esta pantalla.
    Task<Result<IReadOnlyList<FiltroItem>>> ObtenerModulosAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>> ObtenerAccionesAsync(int? idModulo, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>> ObtenerUsuariosAsync(CancellationToken ct = default);
}
