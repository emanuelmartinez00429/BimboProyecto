using CapaAplicacion.Common;
using CapaAplicacion.Common.Cache;
using CapaAplicacion.Conexion;
using CapaAplicacion.Usuarios.Dtos;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDatos.Cache;
using CapaDatos.Modelados.Usuarios;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;
using Op = Supabase.Postgrest.Constants.Operator;
using Ord = Supabase.Postgrest.Constants.Ordering;

namespace CapaDatos.Repositories.Usuarios;

public sealed class RolRepository : RepositorioBase, IRolRepository
{
    private const int Activo = 1;
    private readonly ICacheService _cache;

    public RolRepository(IConexionMonitor conexion, ICacheService cache) : base(conexion)
    {
        _cache = cache;
    }

    public Task<Result<IReadOnlyList<RolDto>>> ObtenerTodosAsync(
        bool incluirInactivos = false,
        CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            var cacheKey = $"catalogos:roles:{(incluirInactivos ? "todos" : "activos")}";
            var resultado = await _cache.ObtenerOCrearAsync(
                cacheKey,
                async _ =>
                {
                    var client = await ConexionSupabase.GetClientAsync().ConfigureAwait(false);
                    var query = client.From<Roles>().Order("nombre_rol", Ord.Ascending);
                    if (!incluirInactivos)
                        query = query.Filter("id_estado", Op.Equals, Activo.ToString());

                    // Factoría de FusionCache ejecutada con CancellationToken.None: regla de oro ADR-026.
                    // Garantiza protección Single-Flight para que cancelaciones rápidas al alternar pestañas
                    // no aborten la consulta compartida en vuelo ni disparen TaskCanceledException.
                    var res = await query.Get(CancellationToken.None).ConfigureAwait(false);
                    IReadOnlyList<RolDto> lista = (res?.Models ?? new List<Roles>())
                        .Select(Mapear)
                        .ToList();
                    return Result<IReadOnlyList<RolDto>>.Ok(lista);
                },
                PoliticasCache.Rbac,
                etiquetas: TagsCache.DeCatalogo(TagsCache.TablaRoles),
                ct: ct);

            return resultado.Value ?? (IReadOnlyList<RolDto>)Array.Empty<RolDto>();
        }, "Obtener roles");

    public Task<Result<RolDto>> CrearAsync(string nombreRol, CancellationToken ct = default) =>
        EjecutarAsync("crear_rol_seguro", new Dictionary<string, object?>
        {
            ["p_nombre_rol"] = nombreRol,
            ["p_id_solicitud"] = Guid.NewGuid(),
        }, "Crear rol", ct);

    public Task<Result<RolDto>> ActualizarAsync(int idRol, string nombreRol, CancellationToken ct = default) =>
        EjecutarAsync("actualizar_rol_seguro", new Dictionary<string, object?>
        {
            ["p_id_rol"] = idRol,
            ["p_nombre_rol"] = nombreRol,
            ["p_id_solicitud"] = Guid.NewGuid(),
        }, "Modificar rol", ct);

    public Task<Result<RolDto>> CambiarEstadoAsync(int idRol, int idEstado, CancellationToken ct = default) =>
        EjecutarAsync("cambiar_estado_rol_seguro", new Dictionary<string, object?>
        {
            ["p_id_rol"] = idRol,
            ["p_id_estado"] = idEstado,
            ["p_id_solicitud"] = Guid.NewGuid(),
        }, "Cambiar estado del rol", ct);

    private Task<Result<RolDto>> EjecutarAsync(
        string funcion,
        Dictionary<string, object?> parametros,
        string contexto,
        CancellationToken ct) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc(funcion, parametros);
            ct.ThrowIfCancellationRequested();

            var token = JToken.Parse(response.Content ?? "{}");
            if (token.Type == JTokenType.String)
                token = JToken.Parse(token.Value<string>() ?? "{}");
            var item = token as JObject ?? token.Children<JObject>().FirstOrDefault()
                ?? throw new InvalidOperationException("Supabase no devolvió el rol actualizado.");

            var rolActualizado = new RolDto
            {
                IdRol = item["id_rol"]?.Value<int>() ?? 0,
                NombreRol = item["nombre_rol"]?.Value<string>() ?? string.Empty,
                IdEstado = item["id_estado"]?.Value<int>() ?? Activo,
                EsSistema = item["es_sistema"]?.Value<bool>() ?? false,
                UsuariosAsignados = item["usuarios_asignados"]?.Value<int>() ?? 0,
            };

            _cache.InvalidarEtiqueta(TagsCache.DeTabla(TagsCache.TablaRoles));
            return rolActualizado;
        }, contexto);

    private static RolDto Mapear(Roles r) => new()
    {
        IdRol = r.idRol,
        NombreRol = r.nombreRol,
        IdEstado = r.idEstado,
        EsSistema = r.esSistema,
    };
}
