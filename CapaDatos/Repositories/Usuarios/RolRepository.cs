using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Usuarios;

public class RolRepository : RepositorioBase, IRolRepository
{
    public RolRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var client    = await ConexionSupabase.GetClientAsync();
            var resultado = await client
                .From<Roles>()
                .Order("nombre_rol", Ord.Ascending)
                .Get();
            var models = resultado?.Models ?? new List<Roles>();
            return (IReadOnlyList<RolDto>)models
                .Select(r => new RolDto
                {
                    IdRol     = r.idRol,
                    NombreRol = r.nombreRol,
                })
                .ToList();
        }, "Obtener roles");
}
