using CapaAplicacion.Common;
using CapaDatos.Modelados.Usuarios;
using CapaDominio.Entities;
using Supabase.Postgrest;
using Supabase.Postgrest.Interfaces;
using Op = Supabase.Postgrest.Constants.Operator;

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
        var client   = await GetClientAsync();
        var aguja    = TextoBusqueda.Normalizar(term);
        var response = await client
            .From<Empleados>()
            .Select("*")
            .Filter("busqueda_empleado", Op.ILike, $"%{aguja}%")
            .Get();
        return response.Models.Select(MapToDomain).ToList();
    }
}
