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
            "{\"id_registro_origen\":42,\"nombre_registro_origen\":\"Ana López\",\"secreto\":\"no mostrar\",\"objeto\":{\"x\":1}}",
            "USUARIO_MODIFICADO");

        var campos = NotificacionMetadataRenderer.Renderizar(notificacion);

        Assert.Contains(campos, x => x is { Etiqueta: "Registro relacionado", Valor: "Ana López" });
        Assert.DoesNotContain(campos, x => x.Valor == "42");
        Assert.DoesNotContain(campos, x => x.Etiqueta is "Secreto" or "Objeto");
    }

    [Fact]
    public void MetadataConocida_SinNombreNoExponeElIdTecnico()
    {
        var campos = NotificacionMetadataRenderer.Renderizar(CrearNotificacion(
            "{\"id_registro_origen\":42}",
            "ROL_MODIFICADO"));

        Assert.DoesNotContain(campos, x => x.Etiqueta == "Registro relacionado");
        Assert.DoesNotContain(campos, x => x.Valor == "42");
    }

    [Fact]
    public void Migracion_EnriqueceMetadataConNombreSinAlterarElHistorial()
    {
        var sql = LeerRepositorio(
            "supabase", "migrations", "20260921141136_mostrar_nombre_registro_en_notificaciones.sql");
        var compacto = Regex.Replace(sql, @"\s+", " ").ToLowerInvariant();

        Assert.Contains("create or replace function public.listar_mis_notificaciones", compacto);
        Assert.Contains("'nombre_registro_origen'", compacto);
        Assert.Contains("empleado_origen.nombre_empleado", compacto);
        Assert.Contains("empleado_origen.apellido_empleado", compacto);
        Assert.Contains("usuario_origen.alias_usuario", compacto);
        Assert.Contains("rol_origen.nombre_rol", compacto);
        Assert.Contains("coalesce(n.metadata,'{}'::jsonb) ||", compacto);
        Assert.DoesNotContain("update public.notificaciones", compacto);
        Assert.Contains("security definer", compacto);
        Assert.Contains("set search_path=pg_catalog,pg_temp", compacto);
        Assert.Contains("grant execute on function public.listar_mis_notificaciones", compacto);
    }

    [Fact]
    public void MetadataDesconocida_SoloMuestraEscalaresYLimitaLaCantidad()
    {
        var notificacion = CrearNotificacion("{\"uno\":1,\"dos\":2,\"tres\":3,\"cuatro\":4,\"cinco\":5,\"seis\":6,\"siete\":7,\"ocho\":8,\"nueve\":9,\"objeto\":{\"x\":1},\"lista\":[1]}");

        var metadata = NotificacionMetadataRenderer.Renderizar(notificacion).Skip(3).ToArray();

        Assert.Equal(8, metadata.Length);
        Assert.DoesNotContain(metadata, x => x.Etiqueta is "Objeto" or "Lista" or "Nueve");
    }

    [Fact]
    public void MetadataDesconocida_MuestraNombreRelacionadoPeroOcultaSuId()
    {
        var campos = NotificacionMetadataRenderer.Renderizar(CrearNotificacion(
            "{\"id_registro_origen\":7,\"nombre_registro_origen\":\"Supervisores\"}"));

        Assert.Contains(campos, x => x is { Etiqueta: "Registro relacionado", Valor: "Supervisores" });
        Assert.DoesNotContain(campos, x => x.Valor == "7");
        Assert.DoesNotContain(campos, x => x.Etiqueta == "Id registro origen");
    }

    [Fact]
    public void CuerpoDetalle_NoRepiteFechaNiSeveridadDelResumen()
    {
        var campos = NotificacionMetadataRenderer.RenderizarCuerpo(CrearNotificacion(metadata: null));

        Assert.Equal(["Tipo"], campos.Select(x => x.Etiqueta));
    }

    [Fact]
    public void Vista_UsaFiltrosCompartidosYFluidos()
    {
        var xaml = LeerRepositorio(
            "CapaUI", "Formularios", "Principal", "Pantallas", "Notificaciones", "NotificacionesView.xaml");

        Assert.Contains("<controls:PanelFiltrosFluido", xaml, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(xaml, "StaticResource ComboFiltroBox").Count);
        Assert.DoesNotContain("Width=\"145\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Height=\"34\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Detalle_UsaModalOverlayYColoresDinamicos()
    {
        var modal = LeerRepositorio(
            "CapaUI", "Formularios", "Principal", "Pantallas", "Notificaciones", "NotificacionDetalleModal.xaml");
        var mainXaml = LeerRepositorio("CapaUI", "Formularios", "Principal", "MainWindow.xaml");
        var mainCode = LeerRepositorio("CapaUI", "Formularios", "Principal", "MainWindow.xaml.cs");

        Assert.StartsWith("<UserControl", modal.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("DynamicResource EmpresaPrimaryDarkBrush", modal, StringComparison.Ordinal);
        Assert.Contains("DynamicResource EmpresaPrimaryDarkerBrush", modal, StringComparison.Ordinal);
        Assert.Contains("DynamicResource EmpresaPrimaryLightBrush", modal, StringComparison.Ordinal);
        Assert.Contains("NotifPuntoSeveridad", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"NotificacionModalOverlay\"", mainXaml, StringComparison.Ordinal);
        Assert.Contains("ModalLayout.LimitarAlOverlay", mainCode, StringComparison.Ordinal);
        Assert.DoesNotContain("NotificacionDetalleWindow", mainCode, StringComparison.Ordinal);
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

    private static string LeerRepositorio(params string[] segmentos) =>
        File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. segmentos]));
}
