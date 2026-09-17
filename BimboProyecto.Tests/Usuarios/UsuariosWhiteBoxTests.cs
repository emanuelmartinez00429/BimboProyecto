using System;
using System.IO;
using System.Reflection;
using CapaAplicacion.Common.Cache;
using CapaDatos.Cache;
using Xunit;

namespace BimboProyecto.Tests.Usuarios;

/// <summary>
/// Pruebas de caja blanca y verificación de invariantes arquitecturales para el Módulo de Usuarios.
/// Verifica la implementación de optimizaciones AP-01 a AP-06, ADR-026 (caché), ADR-028 (WPF)
/// y P-060/P-061 (concurrencia lock-free y manejo honesto de fallas parciales).
/// </summary>
public sealed class UsuariosWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES VISUALES Y CONVENCIONES DE TABLA
    // =========================================================================

    [Fact(DisplayName = "UsuariosView alinea columnas a la izquierda y elimina overrides locales de encabezado")]
    public void UsuariosView_AlineacionColumnasIzquierda_Y_SinOverrideLocalDeHeader()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Ausencia absoluta de estilos derechos residuales
        Assert.DoesNotContain("HeaderDerecho", xaml);
        Assert.DoesNotContain("CeldaDerecha", xaml);

        // 2. Ausencia de override local a DataGridColumnHeader (debe heredar de Styles.xaml)
        Assert.DoesNotContain("<Style TargetType=\"DataGridColumnHeader\">", xaml);

        // 3. CeldaIzquierda como estilo por defecto del DataGrid
        Assert.Contains("CellStyle=\"{StaticResource CeldaIzquierda}\"", xaml);

        // 4. Columnas de texto y fecha alineadas a la izquierda con CeldaIzquierda
        Assert.Matches(@"Header=""EMPLEADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""EMAIL""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ROL""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ÚLTIMO ACCESO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""CREADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ACTUALIZADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);

        // 5. Columnas especiales (# y ESTADO) debidamente centradas
        Assert.Matches(@"Header=""#""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
        Assert.Matches(@"Header=""ESTADO""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
    }

    // =========================================================================
    // 2. INVARIANTES DE ENLACE DECLARATIVO DE FILTROS (AP-02)
    // =========================================================================

    [Fact(DisplayName = "UsuariosView implementa RadioButtons declarativos sin GroupName ni supresores imperativos (AP-02)")]
    public void UsuariosView_RadioButtonsDeclarativos_SinGroupNameNiSupresionImperativa()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml");
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml.cs");
        if (!File.Exists(archivoXaml) || !File.Exists(archivoCs)) return;

        var xaml = File.ReadAllText(archivoXaml);
        var cs = File.ReadAllText(archivoCs);

        // 1. RadioButtons omiten GroupName para eliminar O(N) visual tree search (AP-02)
        Assert.DoesNotContain("GroupName=\"EstadoFiltro\"", xaml);

        // 2. RadioButtons enlazan TwoWay mediante EnumToBooleanConverter
        Assert.Contains("EnumToBooleanConverter.Instancia", xaml);
        Assert.Contains("EstadoUsuarioFilter.Activos", xaml);
        Assert.Contains("EstadoUsuarioFilter.Inactivos", xaml);
        Assert.Contains("EstadoUsuarioFilter.Todos", xaml);

        // 3. Code-behind libre de supresores manuales y eventos Checked imperativos
        Assert.DoesNotContain("_suppressFilterChange", cs);
        Assert.DoesNotContain("EstadoFiltro_Changed", cs);
    }

    // =========================================================================
    // 3. INVARIANTES DE OVERLAYS REUTILIZABLES (AP-04)
    // =========================================================================

    [Fact(DisplayName = "UsuariosView utiliza LoadingOverlay y EmptyStateOverlay declarativos (AP-04)")]
    public void UsuariosView_UtilizaOverlaysDeclarativos_SinAnimacionImperativa()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml");
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosView.xaml.cs");
        if (!File.Exists(archivoXaml) || !File.Exists(archivoCs)) return;

        var xaml = File.ReadAllText(archivoXaml);
        var cs = File.ReadAllText(archivoCs);

        // 1. Presencia de controles declarativos
        Assert.Contains("controls:LoadingOverlay", xaml);
        Assert.Contains("controls:EmptyStateOverlay", xaml);
        Assert.Contains("IsLoading=\"{Binding IsLoading}\"", xaml);
        Assert.Contains("EstaVacio=\"{Binding NoResults}\"", xaml);

        // 2. Ausencia de animaciones imperativas de spinner en code-behind
        Assert.DoesNotContain("_spinnerStory", cs);
        Assert.DoesNotContain("IniciarSpinner", cs);
        Assert.DoesNotContain("DetenerSpinner", cs);
        Assert.DoesNotContain("ActualizarCarga", cs);
    }

    // =========================================================================
    // 4. INVARIANTES DE CONCURRENCIA Y CANCELACIÓN SEGURA (AP-05, P-060)
    // =========================================================================

    [Fact(DisplayName = "UsuariosViewModel coordina cancelación lock-free y omite Dispose en finally (AP-05, P-060)")]
    public void UsuariosViewModel_CoordinaCancelacionLockFree_Y_OmiteDisposeEnFinally()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuariosViewModel.cs");
        if (!File.Exists(archivoVm)) return;

        var vmCs = File.ReadAllText(archivoVm);

        // 1. Cancelación atómica lock-free
        Assert.Contains("Interlocked.Exchange(ref _ctsPagina", vmCs);
        Assert.Contains("CancelAsync()", vmCs);
        Assert.Contains("catch (ObjectDisposedException)", vmCs);

        // 2. Protocolo P-060: nunca llamar cts.Dispose() en finally
        Assert.Contains("Interlocked.CompareExchange(ref _ctsPagina, null, cts);", vmCs);
        Assert.DoesNotMatch(@"finally\s*\{[^}]*cts\.Dispose\(\)", vmCs);

        // 3. Notificación de reseteo de filtros
        Assert.Contains("OnPropertyChanged(nameof(EstadoFiltro));", vmCs);
        Assert.Contains("OnPropertyChanged(nameof(RolFiltro));", vmCs);
    }

    // =========================================================================
    // 5. INVARIANTES DE MODALES Y DIRTY TRACKING (AP-01, AP-03, AP-06, P-061)
    // =========================================================================

    [Fact(DisplayName = "UsuarioModal desacopla DropShadowEffect y congela geometrías vectoriales (AP-06)")]
    public void UsuarioModal_DesacoplaDropShadowDeClipToBounds_Y_CongelaGeometrias()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuarioModal.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Sombra desacoplada en borde hermano
        Assert.Contains("<!-- Modal container with decoupled drop shadow (AP-06) -->", xaml);
        Assert.Contains("<DropShadowEffect Color=\"Black\"", xaml);

        // 2. El contenedor con ClipToBounds no aloja DropShadowEffect directamente
        Assert.DoesNotMatch(@"<Border[^>]*ClipToBounds=""True""[^>]*>\s*<Border\.Effect>", xaml);

        // 3. Geometrías vectoriales congeladas
        Assert.Contains("po:Freeze=\"True\"", xaml);
    }

    [Fact(DisplayName = "UsuarioModal implementa ChangeTracker, RPCs selectivas y reporte honesto de fallas (AP-03, P-061)")]
    public void UsuarioModal_ImplementaChangeTracker_Y_ManejoHonestoDeFallas()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Usuarios", "UsuarioModal.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var cs = File.ReadAllText(archivoCs);

        // 1. ChangeTracker tipado
        Assert.Contains("ChangeTracker<UsuarioSnapshot>", cs);
        Assert.Contains("_tracker.IsDirty(snapshotActual)", cs);

        // 2. Manejo honesto de fallas parciales (P-061)
        Assert.Contains("El rol del usuario se actualizó correctamente, pero no se pudo cambiar el estado", cs);
        Assert.Contains("_solicitud.Confirmar();", cs);
    }

    // =========================================================================
    // 6. INVARIANTES DE CACHÉ REALTIME Y SUGERENCIAS (ADR-026)
    // =========================================================================

    [Fact(DisplayName = "TagsCache e InvalidadorCacheRealtime incluyen la tabla usuarios (ADR-026)")]
    public void TagsCache_E_InvalidadorCache_RegistranTablaUsuarios()
    {
        Assert.Equal("usuarios", TagsCache.TablaUsuarios);

        var archivoInvalidador = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaDatos", "Cache", "InvalidadorCacheRealtime.cs");
        if (File.Exists(archivoInvalidador))
        {
            var cs = File.ReadAllText(archivoInvalidador);
            Assert.Contains("TagsCache.TablaUsuarios", cs);
        }
    }

    [Fact(DisplayName = "UsuarioRepository implementa BuscarSugerenciasAsync e invalidación de caché (ADR-026)")]
    public void UsuarioRepository_ImplementaBuscarSugerencias_E_InvalidacionDeCache()
    {
        var archivoRepo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaDatos", "Repositories", "Usuarios", "UsuarioRepository.cs");
        if (!File.Exists(archivoRepo)) return;

        var cs = File.ReadAllText(archivoRepo);

        Assert.Contains("BuscarSugerenciasAsync", cs);
        Assert.Contains("TagsCache.DeCatalogo(TagsCache.TablaUsuarios)", cs);
        Assert.Contains("InvalidarEtiqueta", cs);
    }
}
