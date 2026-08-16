using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Contactos.Proveedores.Dtos;
using CapaAplicacion.Contactos.Proveedores.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Contactos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Op  = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Contactos;

public class ContactoProveedorCrudRepository : RepositorioBase, IContactoProveedorRepository
{
    private readonly IUsuarioSesionService _sesionService;

    public ContactoProveedorCrudRepository(IConexionMonitor conexion, IUsuarioSesionService sesionService)
        : base(conexion) => _sesionService = sesionService;

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
            ct.ThrowIfCancellationRequested();
            int idUsuario = _sesionService.SesionActual?.IdUsuario
                ?? throw new InvalidOperationException("No hay una sesión activa; no se puede crear el contacto de proveedor.");

            var client = await ConexionSupabase.GetClientAsync();
            var parametros = new Dictionary<string, object?>
            {
                ["p_id_proveedor"] = dto.IdProveedor,
                ["p_nombre_contacto"] = dto.Nombre,
                ["p_telefono_contacto"] = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono,
                ["p_correo_contacto"] = string.IsNullOrWhiteSpace(dto.Correo) ? null : dto.Correo,
                ["p_id_estado"] = EstadoRegistro.Activo,
                ["p_usuario_ingresando"] = idUsuario,
            };

            var response = await client.Rpc("ingresar_contacto_proveedor_tabla_bitacora", parametros);
            ct.ThrowIfCancellationRequested();
            return ObtenerIdCreado(response?.Content);
        }, "Crear contacto proveedor");

    private static int ObtenerIdCreado(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("La función de creación no devolvió el identificador del contacto de proveedor.");
        int id;
        try { id = JToken.Parse(json).ToObject<int>(); }
        catch (Exception ex) when (ex is JsonException or FormatException)
        { throw new InvalidOperationException("La función de creación devolvió un identificador inválido.", ex); }
        return id > 0 ? id : throw new InvalidOperationException("La función de creación devolvió un identificador inválido.");
    }

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
