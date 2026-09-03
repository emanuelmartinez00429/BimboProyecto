using CapaAplicacion.Common;
using CapaAplicacion.Conexion;
using CapaAplicacion.Notificaciones.Dtos;
using CapaAplicacion.Notificaciones.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositories.Notificaciones;

public sealed class NotificacionRepository : RepositorioBase, INotificacionRepository
{
    public NotificacionRepository(IConexionMonitor conexion) : base(conexion) { }

    public Task<Result<IReadOnlyList<NotificacionDto>>> ListarAsync(
        FiltroNotificacionesDto filtro, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("listar_mis_notificaciones", new Dictionary<string, object?>
            {
                ["p_estado_bandeja"] = filtro.EstadoBandeja,
                ["p_severidad"] = filtro.Severidad,
                ["p_codigo_tipo"] = filtro.CodigoTipo,
                ["p_limite"] = filtro.Limite,
                ["p_cursor_fecha"] = filtro.CursorFecha,
                ["p_cursor_id"] = filtro.CursorId,
            });
            return (IReadOnlyList<NotificacionDto>)LeerObjetos(response.Content).Select(Mapear).ToList();
        }, "Consultar notificaciones");

    public Task<Result<NotificacionDto?>> ObtenerAsync(long idNotificacion, CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("obtener_mi_notificacion", new Dictionary<string, object?>
            {
                ["p_id_notificacion"] = idNotificacion,
            });
            return LeerObjetos(response.Content).Select(Mapear).FirstOrDefault();
        }, "Consultar notificación");

    public Task<Result<long>> ContarNoLeidasAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("contar_mis_notificaciones_no_leidas", new { });
            var token = LeerToken(response.Content);
            return token.Type == JTokenType.Array ? token.First?.Value<long>() ?? 0 : token.Value<long>();
        }, "Contar notificaciones no leídas");

    public Task<Result> MarcarLeidaAsync(long idNotificacion, CancellationToken ct = default) =>
        EjecutarAsync("marcar_notificacion_leida", idNotificacion, "Marcar notificación como leída", ct);

    public Task<Result> ArchivarAsync(long idNotificacion, CancellationToken ct = default) =>
        EjecutarAsync("archivar_mi_notificacion", idNotificacion, "Archivar notificación", ct);

    public Task<Result> RestaurarAsync(long idNotificacion, CancellationToken ct = default) =>
        EjecutarAsync("restaurar_mi_notificacion", idNotificacion, "Restaurar notificación", ct);

    public Task<Result<int>> MarcarTodasLeidasYArchivarAsync(CancellationToken ct = default) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            var response = await client.Rpc("marcar_todas_mis_notificaciones_leidas", new { });
            return LeerToken(response.Content).Value<int>();
        }, "Marcar todas las notificaciones como leídas y archivar");

    private Task<Result> EjecutarAsync(string rpc, long id, string contexto, CancellationToken ct) =>
        TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            var client = await ConexionSupabase.GetClientAsync();
            await client.Rpc(rpc, new Dictionary<string, object?> { ["p_id_notificacion"] = id });
        }, contexto);

    private static JToken LeerToken(string? contenido)
    {
        var token = JToken.Parse(string.IsNullOrWhiteSpace(contenido) ? "null" : contenido);
        return token.Type == JTokenType.String
            ? JToken.Parse(token.Value<string>() ?? "null")
            : token;
    }

    private static IEnumerable<JObject> LeerObjetos(string? contenido)
    {
        var token = LeerToken(contenido);
        if (token is JObject objeto) return new[] { objeto };
        return token.Children<JObject>();
    }

    private static NotificacionDto Mapear(JObject x) => new()
    {
        IdNotificacion = x["id_notificacion"]?.Value<long>() ?? 0,
        CodigoTipo = x["codigo_tipo"]?.Value<string>() ?? string.Empty,
        NombreTipo = x["nombre_tipo"]?.Value<string>() ?? string.Empty,
        Titulo = x["titulo"]?.Value<string>() ?? string.Empty,
        Mensaje = x["mensaje"]?.Value<string>() ?? string.Empty,
        Severidad = x["severidad"]?.Value<string>() ?? "informativa",
        EstadoNotificacion = x["estado_notificacion"]?.Value<string>() ?? "activa",
        FechaCreacion = x["fecha_creacion"]?.Value<DateTime>() ?? DateTime.MinValue,
        FechaLeida = x["fecha_leida"]?.Type is null or JTokenType.Null ? null : x["fecha_leida"]!.Value<DateTime>(),
        FechaArchivada = x["fecha_archivada"]?.Type is null or JTokenType.Null ? null : x["fecha_archivada"]!.Value<DateTime>(),
        Actor = x["actor"]?.Value<string>() ?? string.Empty,
        IdModulo = x["id_modulo"]?.Type is null or JTokenType.Null ? null : x["id_modulo"]!.Value<int>(),
        IdAccion = x["id_accion"]?.Type is null or JTokenType.Null ? null : x["id_accion"]!.Value<int>(),
        TablaOrigen = x["tabla_origen"]?.Value<string>(),
        IdRegistroOrigen = x["id_registro_origen"]?.Type is null or JTokenType.Null ? null : x["id_registro_origen"]!.Value<int>(),
        MetadataJson = x["metadata"]?.Type is null or JTokenType.Null ? null : x["metadata"]!.ToString(Formatting.None),
    };
}
