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
    // Caché de proceso: la lista de roles no tiene CRUD propio todavía, así que
    // no cambia durante la sesión. Ver la misma estrategia en RolPermisoRepository.
    private static IReadOnlyList<RolDto>? _rolesCache;
    private static readonly SemaphoreSlim RolesLock = new(1, 1);

    public RolRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            if (_rolesCache is not null)
                return _rolesCache;

            await RolesLock.WaitAsync(ct);
            try
            {
                if (_rolesCache is not null)
                    return _rolesCache;

                var client    = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Roles>()
                    .Order("nombre_rol", Ord.Ascending)
                    .Get();
                var models = resultado?.Models ?? new List<Roles>();
                _rolesCache = models
                    .Select(r => new RolDto
                    {
                        IdRol     = r.idRol,
                        NombreRol = r.nombreRol,
                    })
                    .ToList();

                return _rolesCache;
            }
            finally
            {
                RolesLock.Release();
            }
        }, "Obtener roles");
}
