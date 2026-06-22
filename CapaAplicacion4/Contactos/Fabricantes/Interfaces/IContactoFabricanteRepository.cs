using CapaAplicacion.Common;
using CapaAplicacion.Contactos.Fabricantes.Dtos;

namespace CapaAplicacion.Contactos.Fabricantes.Interfaces;

public interface IContactoFabricanteRepository
{
    Task<Result<IReadOnlyList<ContactoFabricanteDto>>> GetByFabricanteAsync(int idFabricante, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ContactoFabricanteDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(ContactoFabricanteDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,                    CancellationToken ct = default);
}
