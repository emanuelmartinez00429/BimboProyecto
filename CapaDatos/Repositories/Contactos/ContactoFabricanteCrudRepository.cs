using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Fabricantes.Dtos;
using CapaAplicacion.Contactos.Fabricantes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Contactos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Contactos;

public class ContactoFabricanteCrudRepository : RepositorioBase, IContactoFabricanteRepository
{
    private readonly IUsuarioSesionService _sesionService;

    public ContactoFabricanteCrudRepository(IConexionMonitor conexion, IUsuarioSesionService sesionService)
        : base(conexion) => _sesionService = sesionService;

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
            ct.ThrowIfCancellationRequested();
            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No hay una sesión activa; no se puede crear el contacto de fabricante.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_fabricante"] = dto.IdFabricante,
                ["p_nombre_contacto"] = dto.Nombre,
                ["p_telefono_contacto"] = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono,
                ["p_correo_contacto"] = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo,
                ["p_id_estado"] = EstadoRegistro.Activo,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc("ingresar_contacto_fabricante_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content);
        }, "Crear contacto fabricante");

    private static int ObtenerIdCreado(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("La función de creación no devolvió el identificador del contacto de fabricante.");
        int id;
        try { id = JToken.Parse(json).ToObject<int>(); }
        catch (Exception ex) when (ex is JsonException or FormatException)
        { throw new InvalidOperationException("La función de creación devolvió un identificador inválido.", ex); }
        return id > 0 ? id : throw new InvalidOperationException("La función de creación devolvió un identificador inválido.");
    }

    public Task<Result> UpdateAsync(ContactoFabricanteDto dto, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.From<ContactoFabricanteModel>()
                .Where(c => c.idContactoFabricante == dto.Id)
                .Set(c => c.nombreContacto,    dto.Nombre)
                .Set(c => c.telefonoContacto!, string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono)
                .Set(c => c.correoContacto!,   string.IsNullOrWhiteSpace(dto.Correo)   ? null : dto.Correo)
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
