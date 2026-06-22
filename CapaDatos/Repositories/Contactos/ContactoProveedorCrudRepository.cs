using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Proveedores.Dtos;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaDatos.Modelados.Contactos;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Contactos;

public class ContactoProveedorCrudRepository : RepositorioBase, IContactoProveedorRepository
{
    public ContactoProveedorCrudRepository(IConexionMonitor conexion) : base(conexion) { }

    private static ContactoProveedorDto Map(ContactoProveedorModel c) => new()
    {
        Id          = c.idContactoProveedor,
        IdProveedor = c.idProveedor,
        Nombre      = c.nombreContacto   ?? string.Empty,
        Telefono    = c.telefonoContacto ?? string.Empty,
        Correo      = c.correoContacto   ?? string.Empty,
        IdEstado    = c.idEstado,
    };

    public Task<Result<IReadOnlyList<ContactoProveedorDto>>> GetByProveedorAsync(
        int idProveedor, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var result = await client.From<ContactoProveedorModel>()
                .Filter("id_proveedor", Op.Equals, idProveedor.ToString())
                .Filter("id_estado",    Op.Equals, EstadoRegistro.Activo.ToString())
                .Order("nombre_contacto", Ord.Ascending)
                .Get();
            return (IReadOnlyList<ContactoProveedorDto>)(result?.Models.Select(Map).ToList()
                   ?? new List<ContactoProveedorDto>());
        }, "Cargar contactos proveedor");

    public Task<Result<int>> CreateAsync(ContactoProveedorDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nuevo  = new ContactoProveedorModel
            {
                idProveedor      = dto.IdProveedor,
                nombreContacto   = dto.Nombre,
                telefonoContacto = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono,
                correoContacto   = string.IsNullOrWhiteSpace(dto.Correo)   ? null : dto.Correo,
                idEstado         = EstadoRegistro.Activo,
            };
            var resultado = await client.From<ContactoProveedorModel>().Insert(nuevo);
            return resultado.Models.First().idContactoProveedor;
        }, "Crear contacto proveedor");

    public Task<Result> UpdateAsync(ContactoProveedorDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<ContactoProveedorModel>()
                .Where(c => c.idContactoProveedor == dto.Id)
                .Set(c => c.nombreContacto,   dto.Nombre)
                .Set(c => c.telefonoContacto, string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono)
                .Set(c => c.correoContacto,   string.IsNullOrWhiteSpace(dto.Correo)   ? null : dto.Correo)
                .Update();
        }, "Actualizar contacto proveedor");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<ContactoProveedorModel>()
                .Where(c => c.idContactoProveedor == id)
                .Set(c => c.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar contacto proveedor");
}
