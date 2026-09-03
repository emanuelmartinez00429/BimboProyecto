using System.Text.RegularExpressions;
using CapaAplicacion.Notificaciones.Dtos;
using CapaAplicacion.Notificaciones.Interfaces;
using CapaUI.Formularios.Principal.Pantallas.Notificaciones;
using Xunit;

namespace BimboProyecto.Tests.Notificaciones;

public sealed class ContratoNotificacionesTests
{
    [Fact]
    public void Migracion_MarcaLecturaYArchivoEnUnSoloUpdate()
    {
        var sql = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "supabase", "migrations", "20260903075117_archivar_notificaciones_al_marcar_todas.sql"));
        var compacto = Regex.Replace(sql, @"\s+", " ").ToLowerInvariant();

        Assert.Single(Regex.Matches(compacto, @"\bupdate public\.notificaciones_usuario\b").Cast<Match>());
        Assert.Contains("fecha_leida=coalesce(fecha_leida,now())", compacto);
        Assert.Contains("fecha_archivada=coalesce(fecha_archivada,now())", compacto);
        Assert.Contains("where id_usuario=v_usuario and fecha_archivada is null", compacto);
        Assert.DoesNotContain("fecha_leida is null", compacto);
        Assert.DoesNotContain(" grant ", compacto);
        Assert.DoesNotContain(" revoke ", compacto);
    }

    [Fact]
    public void Repositorio_ExponeLaOperacionConSemanticaCompleta()
    {
        Assert.NotNull(typeof(INotificacionRepository).GetMethod("MarcarTodasLeidasYArchivarAsync"));
        Assert.Null(typeof(INotificacionRepository).GetMethod("MarcarTodasLeidasAsync"));
    }

    [Fact]
    public void MetadataVacia_ConservaSoloCamposGenerales()
    {
        var campos = NotificacionMetadataRenderer.Renderizar(CrearNotificacion(metadata: null));

        Assert.Equal(["Tipo", "Severidad", "Creada"], campos.Select(x => x.Etiqueta));
    }

    [Fact]
    public void MetadataConocida_NoExponeCamposFueraDelRenderizadorRegistrado()
    {
        var notificacion = CrearNotificacion(
            "{\"id_registro_origen\":42,\"secreto\":\"no mostrar\",\"objeto\":{\"x\":1}}",
            "USUARIO_MODIFICADO");

        var campos = NotificacionMetadataRenderer.Renderizar(notificacion);

        Assert.Contains(campos, x => x is { Etiqueta: "Registro relacionado", Valor: "42" });
        Assert.DoesNotContain(campos, x => x.Etiqueta is "Secreto" or "Objeto");
    }

    [Fact]
    public void MetadataDesconocida_SoloMuestraEscalaresYLimitaLaCantidad()
    {
        var notificacion = CrearNotificacion("{\"uno\":1,\"dos\":2,\"tres\":3,\"cuatro\":4,\"cinco\":5,\"seis\":6,\"siete\":7,\"ocho\":8,\"nueve\":9,\"objeto\":{\"x\":1},\"lista\":[1]}");

        var metadata = NotificacionMetadataRenderer.Renderizar(notificacion).Skip(3).ToArray();

        Assert.Equal(8, metadata.Length);
        Assert.DoesNotContain(metadata, x => x.Etiqueta is "Objeto" or "Lista" or "Nueve");
    }

    private static NotificacionDto CrearNotificacion(string? metadata, string codigoTipo = "TIPO_DESCONOCIDO") => new()
    {
        CodigoTipo = codigoTipo,
        NombreTipo = "Tipo",
        Severidad = "informativa",
        FechaCreacion = new DateTime(2026, 9, 3, 8, 0, 0),
        MetadataJson = metadata,
    };

    private static string EncontrarRaizRepositorio()
    {
        foreach (var inicio in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var directorio = new DirectoryInfo(inicio); directorio is not null; directorio = directorio.Parent)
                if (File.Exists(Path.Combine(directorio.FullName, "BimboProyecto.sln"))) return directorio.FullName;
        throw new DirectoryNotFoundException("No se encontró BimboProyecto.sln para validar notificaciones.");
    }
}
