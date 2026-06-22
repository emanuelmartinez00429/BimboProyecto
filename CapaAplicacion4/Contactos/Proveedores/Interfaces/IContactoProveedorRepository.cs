using CapaAplicacion.Common;
using CapaAplicacion.Contactos.Proveedores.Dtos;

namespace CapaAplicacion.Contactos.Proveedores.Interfaces;

public interface IContactoProveedorRepository
{
    Task<Result<IReadOnlyList<ContactoProveedorDto>>> GetByProveedorAsync(int idProveedor, CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ContactoProveedorDto dto, CancellationToken ct = default);
    Task<Result>      UpdateAsync(ContactoProveedorDto dto, CancellationToken ct = default);
    Task<Result>      DeleteAsync(int id,                   CancellationToken ct = default);
}
