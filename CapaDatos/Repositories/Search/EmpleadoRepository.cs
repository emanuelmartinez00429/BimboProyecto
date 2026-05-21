using CapaDatos.Modelados.Usuarios;
using CapaDominio.Entities;
using static Supabase.Postgrest.Constants;

namespace CapaDatos.Repositories.Search;

public class EmpleadoRepository
    : SupabaseRepository<Empleado, Empleados>
{
    protected override Empleado MapToDomain(Empleados e) => new()
    {
        Id        = e.idEmpleado,
        Nombres   = e.nombreEmpleado   ?? string.Empty,
        Apellidos = e.apellidoEmpleado ?? string.Empty,
        Identidad = e.numeroIdentidad  ?? string.Empty,
        Telefono  = e.telefonoEmpleado ?? string.Empty,
        Correo    = e.correoEmpleado   ?? string.Empty,
        IdEstado  = e.idEstado,
    };

    public override async Task<IEnumerable<Empleado>> SearchAsync(string term, CancellationToken ct = default)
    {
        var client  = await GetClientAsync();
        var pattern = $"%{term}%";
        var response = await client
            .From<Empleados>()
            .Select("*")
            .Filter("or", Operator.Equals,
                $"(nombre_empleado.ilike.{pattern},apellido_empleado.ilike.{pattern},numero_identidad.ilike.{pattern},correo_empleado.ilike.{pattern})")
            .Get();
        return response.Models.Select(MapToDomain).ToList();
    }
}
