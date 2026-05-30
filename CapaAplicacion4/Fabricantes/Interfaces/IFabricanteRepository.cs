using CapaAplicacion.Common;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Queries;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Queries;

namespace CapaAplicacion.Fabricantes.Interfaces;

public interface IFabricanteRepository
{
    Task<Result<PagedResult<FabricanteDto>>>   GetPagedAsync(int page, int size, FabricanteFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FabricanteDto>>> BuscarSugerenciasAsync(string termino, FabricanteFiltros filtros, CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>>    GetPaisesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<FiltroItem>>>    GetProveedoresAsync(CancellationToken ct = default);
    Task<Result<int>>                          GetPaginaDeRegistroAsync(int id, int size, FabricanteFiltros filtros, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(FabricanteDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(FabricanteDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,            CancellationToken ct = default);
}
