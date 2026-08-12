using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Op = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Usuarios;

public sealed class RolPermisoRepository : RepositorioBase, IRolPermisoRepository
{
    private const int Activo = 1;
    private const int Inactivo = 2;
    private const string PermisoAdministrar = "Modificar Configuración";

    // Caché de proceso: el catálogo de módulos/acciones es prácticamente estático
    // (solo cambia con una migración de esquema). Evita repetir 2 round-trips a
    // Supabase cada vez que el usuario navega a Roles, que era la causa real de
    // los ~3s de pantalla en blanco en cada visita.
    private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache;
    private static readonly SemaphoreSlim CatalogoLock = new(1, 1);

    private readonly IUsuarioSesionService _sesion;

    public RolPermisoRepository(IConexionMonitor conexion, IUsuarioSesionService sesion)
        : base(conexion)
    {
        _sesion = sesion;
    }

    public Task<Result<RolesResumenDto>> ObtenerResumenAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirLectura();

            var client = await ConexionSupabase.GetClientAsync();

            // Las tres consultas salen JUNTAS: abrir la pantalla cuesta el viaje
            // más lento y no la suma de todos.
            var catalogoTask = CargarCatalogoAsync(ct);
            var rolesTask = client.From<Roles>().Order("nombre_rol", Ord.Ascending).Get(ct);
            var asignacionesTask = client
                .From<AccionRol>()
                .Filter("id_estado", Op.Equals, Activo.ToString())
                .Get(ct);

            await Task.WhenAll(catalogoTask, rolesTask, asignacionesTask);

            var accionesPorRol = new Dictionary<int, IReadOnlySet<int>>();
            foreach (var grupo in (asignacionesTask.Result?.Models ?? new List<AccionRol>())
                         .GroupBy(x => x.idRol))
                accionesPorRol[grupo.Key] = grupo.Select(x => x.idAccion).ToHashSet();

            return new RolesResumenDto
            {
                Roles = (rolesTask.Result?.Models ?? new List<Roles>())
                    .Select(r => new RolDto { IdRol = r.idRol, NombreRol = r.nombreRol })
                    .ToList(),
                Modulos = catalogoTask.Result,
                AccionesPorRol = accionesPorRol,
            };
        }, "Cargar configuración de roles");

    public Task<Result<IReadOnlyList<ModuloAccionesDto>>> ObtenerCatalogoAsync(
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirLectura();
            return await CargarCatalogoAsync(ct);
        }, "Cargar catálogo de permisos");

    private static async Task<IReadOnlyList<ModuloAccionesDto>> CargarCatalogoAsync(CancellationToken ct)
    {
        if (_catalogoCache is not null)
            return _catalogoCache;

        await CatalogoLock.WaitAsync(ct);
        try
        {
            if (_catalogoCache is not null)
                return _catalogoCache;

            var client = await ConexionSupabase.GetClientAsync();
            var accionesTask = client.From<Accion>().Get(ct);
            var modulosTask = client.From<Modulo>().Get(ct);
            await Task.WhenAll(accionesTask, modulosTask);

            var acciones = accionesTask.Result?.Models ?? new List<Accion>();
            var modulos = modulosTask.Result?.Models ?? new List<Modulo>();

            _catalogoCache = modulos
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

            return _catalogoCache;
        }
        finally
        {
            CatalogoLock.Release();
        }
    }

    private void ExigirLectura()
    {
        if (!_sesion.TienePermiso("Consultar Usuario") &&
            !_sesion.TienePermiso(PermisoAdministrar))
            throw new UnauthorizedAccessException("No tiene permiso para consultar la configuración de roles.");
    }

    public Task<Result<IReadOnlySet<int>>> ObtenerAccionesAsignadasAsync(
        int idRol,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ExigirLectura();

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
