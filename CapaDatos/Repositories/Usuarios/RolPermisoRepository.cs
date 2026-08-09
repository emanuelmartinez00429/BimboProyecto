using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Op = Supabase.Postgrest.Constants.Operator;

namespace CapaDatos.Repositories.Usuarios;

public sealed class RolPermisoRepository : RepositorioBase, IRolPermisoRepository
{
    private const int Activo = 1;
    private const int Inactivo = 2;
    private const string PermisoAdministrar = "Modificar Configuración";

    private readonly IUsuarioSesionService _sesion;

    public RolPermisoRepository(IConexionMonitor conexion, IUsuarioSesionService sesion)
        : base(conexion)
    {
        _sesion = sesion;
    }

    public Task<Result<IReadOnlyList<ModuloAccionesDto>>> ObtenerCatalogoAsync(
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            if (!_sesion.TienePermiso("Consultar Usuario") &&
                !_sesion.TienePermiso(PermisoAdministrar))
                throw new UnauthorizedAccessException("No tiene permiso para consultar la configuración de roles.");

            var client = await ConexionSupabase.GetClientAsync();
            var accionesTask = client.From<Accion>().Get(ct);
            var modulosTask = client.From<Modulo>().Get(ct);
            await Task.WhenAll(accionesTask, modulosTask);

            var acciones = accionesTask.Result?.Models ?? new List<Accion>();
            var modulos = modulosTask.Result?.Models ?? new List<Modulo>();

            return (IReadOnlyList<ModuloAccionesDto>)modulos
                .OrderBy(m => m.nombreModulo)
                .Select(m => new ModuloAccionesDto
                {
                    IdModulo = m.idModulo,
                    NombreModulo = m.nombreModulo,
                    DescripcionModulo = m.descripcionModulo,
                    Acciones = acciones
                        .Where(a => a.idModulo == m.idModulo)
                        .OrderBy(a => a.nombreAccion)
                        .Select(a => new AccionDto
                        {
                            IdAccion = a.idAccion,
                            NombreAccion = a.nombreAccion,
                            DescripcionAccion = a.descripcionAccion,
                        })
                        .ToList(),
                })
                .Where(m => m.Acciones.Count > 0)
                .ToList();
        }, "Cargar catálogo de permisos");

    public Task<Result<IReadOnlySet<int>>> ObtenerAccionesAsignadasAsync(
        int idRol,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            if (!_sesion.TienePermiso("Consultar Usuario") &&
                !_sesion.TienePermiso(PermisoAdministrar))
                throw new UnauthorizedAccessException("No tiene permiso para consultar la configuración de roles.");

            var client = await ConexionSupabase.GetClientAsync();
            var response = await client
                .From<AccionRol>()
                .Filter("id_rol", Op.Equals, idRol.ToString())
                .Filter("id_estado", Op.Equals, Activo.ToString())
                .Get(ct);

            return (IReadOnlySet<int>)(response?.Models ?? new List<AccionRol>())
                .Select(x => x.idAccion)
                .ToHashSet();
        }, "Cargar permisos del rol");

    public Task<Result> GuardarAsignacionesAsync(
        int idRol,
        IReadOnlyCollection<int> idsAcciones,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            if (!_sesion.TienePermiso(PermisoAdministrar))
                throw new UnauthorizedAccessException("No tiene permiso para modificar roles.");

            var client = await ConexionSupabase.GetClientAsync();
            var response = await client
                .From<AccionRol>()
                .Filter("id_rol", Op.Equals, idRol.ToString())
                .Get(ct);

            var existentes = response?.Models ?? new List<AccionRol>();
            var seleccionadas = idsAcciones.ToHashSet();

            foreach (var relacion in existentes)
            {
                int estadoDeseado = seleccionadas.Contains(relacion.idAccion) ? Activo : Inactivo;
                seleccionadas.Remove(relacion.idAccion);

                if (relacion.idEstado == estadoDeseado)
                    continue;

                await client.From<AccionRol>()
                    .Where(x => x.idAccionRol == relacion.idAccionRol)
                    .Set(x => x.idEstado, estadoDeseado)
                    .Set(x => x.updatedAt!, DateTime.UtcNow)
                    .Update();
            }

            if (seleccionadas.Count > 0)
            {
                var nuevas = seleccionadas.Select(idAccion => new AccionRol
                {
                    idAccion = idAccion,
                    idRol = idRol,
                    idEstado = Activo,
                }).ToList();

                await client.From<AccionRol>().Insert(nuevas);
            }
        }, "Guardar permisos del rol");
}
