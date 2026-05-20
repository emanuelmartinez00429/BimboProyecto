using CapaDatos.Modelados.Usuarios;
using CapaDominio.Entities;

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
}
