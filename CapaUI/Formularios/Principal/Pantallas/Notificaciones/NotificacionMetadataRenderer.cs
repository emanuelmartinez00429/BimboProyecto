using Newtonsoft.Json.Linq;
using CapaAplicacion.Notificaciones.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.Notificaciones;

public sealed record CampoNotificacionDetalle(string Etiqueta, string Valor);

/// <summary>
/// Registro explícito de formatos por código. Nunca presenta objetos JSON ni
/// campos no escalares: la metadata es auxiliar y no una segunda fuente de datos.
/// </summary>
public static class NotificacionMetadataRenderer
{
    private static readonly IReadOnlyDictionary<string, Func<JObject, IReadOnlyList<CampoNotificacionDetalle>>> Renderizadores =
        new Dictionary<string, Func<JObject, IReadOnlyList<CampoNotificacionDetalle>>>(StringComparer.Ordinal)
        {
            ["USUARIO_CREADO"] = RenderizarRegistro,
            ["USUARIO_MODIFICADO"] = RenderizarRegistro,
            ["USUARIO_ACTIVADO"] = RenderizarRegistro,
            ["USUARIO_DESACTIVADO"] = RenderizarRegistro,
            ["ROL_CREADO"] = RenderizarRegistro,
            ["ROL_MODIFICADO"] = RenderizarRegistro,
            ["ROL_ACTIVADO"] = RenderizarRegistro,
            ["ROL_DESACTIVADO"] = RenderizarRegistro,
        };

    public static IReadOnlyList<CampoNotificacionDetalle> Renderizar(NotificacionDto notificacion)
    {
        var generales = new List<CampoNotificacionDetalle>
        {
            new("Tipo", string.IsNullOrWhiteSpace(notificacion.NombreTipo) ? notificacion.CodigoTipo : notificacion.NombreTipo),
            new("Severidad", Etiqueta(notificacion.Severidad)),
            new("Creada", notificacion.FechaCreacion.ToString("dd/MM/yyyy HH:mm")),
        };

        if (!string.IsNullOrWhiteSpace(notificacion.Actor)) generales.Add(new("Actor", notificacion.Actor));
        if (notificacion.FechaLeida is { } leida) generales.Add(new("Leída", leida.ToString("dd/MM/yyyy HH:mm")));
        if (notificacion.FechaArchivada is { } archivada) generales.Add(new("Archivada", archivada.ToString("dd/MM/yyyy HH:mm")));

        if (!TryLeerObjeto(notificacion.MetadataJson, out var metadata)) return generales;
        var campos = Renderizadores.TryGetValue(notificacion.CodigoTipo, out var renderizador)
            ? renderizador(metadata)
            : RenderizarSeguro(metadata);
        generales.AddRange(campos);
        return generales;
    }

    /// <summary>
    /// Devuelve los campos que van debajo del resumen visual del modal. La fecha
    /// y la severidad viven en el hero para no repetir la misma información.
    /// </summary>
    public static IReadOnlyList<CampoNotificacionDetalle> RenderizarCuerpo(NotificacionDto notificacion)
    {
        var campos = Renderizar(notificacion);
        return campos.Where(x => x.Etiqueta is not "Severidad" and not "Creada").ToArray();
    }

    private static IReadOnlyList<CampoNotificacionDetalle> RenderizarRegistro(JObject metadata)
    {
        var nombre = metadata["nombre_registro_origen"]?.Value<string>();
        return string.IsNullOrWhiteSpace(nombre)
            ? []
            : [new CampoNotificacionDetalle("Registro relacionado", nombre)];
    }

    private static IReadOnlyList<CampoNotificacionDetalle> RenderizarSeguro(JObject metadata) => metadata.Properties()
        .Where(x => !string.Equals(x.Name, "id_registro_origen", StringComparison.OrdinalIgnoreCase))
        .Where(x => x.Value.Type is JTokenType.String or JTokenType.Integer or JTokenType.Float or JTokenType.Boolean)
        .Take(8)
        .Select(x => new CampoNotificacionDetalle(
            string.Equals(x.Name, "nombre_registro_origen", StringComparison.OrdinalIgnoreCase)
                ? "Registro relacionado"
                : FormatearEtiqueta(x.Name),
            x.Value.ToString()))
        .ToList();

    private static bool TryLeerObjeto(string? json, out JObject metadata)
    {
        metadata = new JObject();
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { metadata = JObject.Parse(json); return true; }
        catch (Newtonsoft.Json.JsonException) { return false; }
    }

    private static string Etiqueta(string severidad) => severidad switch
    {
        "critica" => "Crítica", "advertencia" => "Advertencia", _ => "Informativa",
    };

    private static string FormatearEtiqueta(string campo) => string.Join(' ', campo.Split('_', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
}
