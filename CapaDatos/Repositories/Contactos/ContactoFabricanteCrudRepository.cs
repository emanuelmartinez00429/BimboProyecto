using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Dtos;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaDatos.Modelados.Contactos;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Contactos;

public class ContactoFabricanteCrudRepository : RepositorioBase, IContactoFabricanteRepository
{
    public ContactoFabricanteCrudRepository(IConexionMonitor conexion) : base(conexion) { }

    private static ContactoFabricanteDto Map(ContactoFabricanteModel c) => new()
    {
        Id           = c.idContactoFabricante,
        IdFabricante = c.idFabricante,
        Nombre       = c.nombreContacto     ?? string.Empty,
        Telefono     = c.telefonoContacto   ?? string.Empty,
        Correo       = c.correoContacto     ?? string.Empty,
        IdEstado     = c.idEstado,
    };

    public Task<Result<IReadOnlyList<ContactoFabricanteDto>>> GetByFabricanteAsync(
        int idFabricante, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var result = await client.From<ContactoFabricanteModel>()
                .Filter("id_fabricante", Op.Equals, idFabricante.ToString())
                .Filter("id_estado",     Op.Equals, EstadoRegistro.Activo.ToString())
                .Order("nombre_contacto", Ord.Ascending)
                .Get();
            return (IReadOnlyList<ContactoFabricanteDto>)(result?.Models.Select(Map).ToList()
                   ?? new List<ContactoFabricanteDto>());
        }, "Cargar contactos fabricante");

    public Task<Result<int>> CreateAsync(ContactoFabricanteDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            var nuevo  = new ContactoFabricanteModel
            {
                idFabricante     = dto.IdFabricante,
                nombreContacto   = dto.Nombre,
                telefonoContacto = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono,
                correoContacto   = string.IsNullOrWhiteSpace(dto.Correo)   ? null : dto.Correo,
                idEstado         = EstadoRegistro.Activo,
            };
            var resultado = await client.From<ContactoFabricanteModel>().Insert(nuevo);
            return resultado.Models.First().idContactoFabricante;
        }, "Crear contacto fabricante");

    public Task<Result> UpdateAsync(ContactoFabricanteDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<ContactoFabricanteModel>()
                .Where(c => c.idContactoFabricante == dto.Id)
                .Set(c => c.nombreContacto,   dto.Nombre)
                .Set(c => c.telefonoContacto, string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono)
                .Set(c => c.correoContacto,   string.IsNullOrWhiteSpace(dto.Correo)   ? null : dto.Correo)
                .Update();
        }, "Actualizar contacto fabricante");

    public Task<Result> DeleteAsync(int id, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<ContactoFabricanteModel>()
                .Where(c => c.idContactoFabricante == id)
                .Set(c => c.idEstado, EstadoRegistro.Inactivo)
                .Update();
        }, "Eliminar contacto fabricante");
}
