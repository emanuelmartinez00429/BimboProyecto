using System;
using System.IO;
using System.Reflection;
using CapaAplicacion.Common.Cache;
using Xunit;

namespace BimboProyecto.Tests.Empleados;

/// <summary>
/// Pruebas de caja blanca y verificación de invariantes arquitecturales para el Módulo de Empleados.
/// Verifica la implementación de optimizaciones AP-01 a AP-06, ADR-026 (caché), ADR-028 (WPF)
/// y P-060/P-061 (concurrencia lock-free y manejo honesto de fallas parciales).
/// </summary>
public sealed class EmpleadosWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES VISUALES Y CONVENCIONES DE TABLA
    // =========================================================================

    [Fact(DisplayName = "EmpleadosView alinea columnas a la izquierda, distribuye ancho completo y activa scroll")]
    public void EmpleadosView_AlineacionColumnasIzquierda_DistribucionAnchoCompleto_Y_Scroll()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Ausencia de override local a DataGridColumnHeader (debe heredar de Styles.xaml)
        Assert.DoesNotContain("<Style TargetType=\"DataGridColumnHeader\">", xaml);

        // 2. Columnas de datos con CeldaIzquierda
        Assert.Matches(@"Header=""EMPLEADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""IDENTIDAD""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""TELÉFONO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""CORREO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);

        // 3. Distribución de ancho completo como en Usuarios (Width="*" y MinWidth="220" en EMPLEADO y CORREO)
        Assert.Matches(@"Header=""EMPLEADO""[^>]*Width=""\*""[^>]*MinWidth=""220""", xaml);
        Assert.Matches(@"Header=""CORREO""[^>]*Width=""\*""[^>]*MinWidth=""220""", xaml);

        // 4. Columnas especiales (# y ESTADO) debidamente centradas
        Assert.Matches(@"Header=""#""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);
        Assert.Matches(@"Header=""ESTADO""[^>]*Width=""70""[^>]*HeaderStyle=""{StaticResource HeaderCentrado}""[^>]*CellStyle=""{StaticResource CeldaCentrada}""", xaml);

        // 5. ScrollViewer horizontal y CanContentScroll
        Assert.Contains(@"ScrollViewer.HorizontalScrollBarVisibility=""Auto""", xaml);
        Assert.Contains(@"ScrollViewer.CanContentScroll=""True""", xaml);
    }

    // =========================================================================
    // 2. INVARIANTES DE ENLACE DECLARATIVO DE FILTROS (AP-02)
    // =========================================================================

    [Fact(DisplayName = "EmpleadosView implementa RadioButtons declarativos sin GroupName ni supresores imperativos (AP-02)")]
    public void EmpleadosView_RadioButtonsDeclarativos_SinGroupNameNiSupresionImperativa()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml");
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml.cs");
        if (!File.Exists(archivoXaml) || !File.Exists(archivoCs)) return;

        var xaml = File.ReadAllText(archivoXaml);
        var cs = File.ReadAllText(archivoCs);

        // 1. RadioButtons omiten GroupName para eliminar O(N) visual tree search (AP-02)
        Assert.DoesNotContain("GroupName=\"EstadoFiltro\"", xaml);

        // 2. RadioButtons enlazan TwoWay mediante EnumToBooleanConverter
        Assert.Contains("EnumToBooleanConverter.Instancia", xaml);
        Assert.Contains("EstadoEmpleadoFilter.Activos", xaml);
        Assert.Contains("EstadoEmpleadoFilter.Inactivos", xaml);
        Assert.Contains("EstadoEmpleadoFilter.Todos", xaml);

        // 3. Code-behind libre de supresores manuales y eventos Checked imperativos
        Assert.DoesNotContain("_suppressFilterChange", cs);
        Assert.DoesNotContain("EstadoFiltro_Changed", cs);
    }

    // =========================================================================
    // 3. INVARIANTES DE OVERLAYS REUTILIZABLES (AP-04)
    // =========================================================================

    [Fact(DisplayName = "EmpleadosView utiliza LoadingOverlay y EmptyStateOverlay declarativos (AP-04)")]
    public void EmpleadosView_UtilizaOverlaysDeclarativos_SinAnimacionImperativa()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml");
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosView.xaml.cs");
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

    [Fact(DisplayName = "EmpleadosViewModel coordina cancelación lock-free y omite Dispose en finally (AP-05, P-060)")]
    public void EmpleadosViewModel_CoordinaCancelacionLockFree_Y_OmiteDisposeEnFinally()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadosViewModel.cs");
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
    }

    // =========================================================================
    // 5. INVARIANTES DE MODALES Y DIRTY TRACKING (AP-01, AP-03, AP-06, P-061)
    // =========================================================================

    [Fact(DisplayName = "EmpleadoModal desacopla DropShadowEffect y congela geometrías vectoriales (AP-06)")]
    public void EmpleadoModal_DesacoplaDropShadowDeClipToBounds_Y_CongelaGeometrias()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadoModal.xaml");
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

    [Fact(DisplayName = "EmpleadoModal implementa ChangeTracker, RPCs selectivas y reporte honesto de fallas (AP-03, P-061)")]
    public void EmpleadoModal_ImplementaChangeTracker_Y_ManejoHonestoDeFallas()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Empleados", "EmpleadoModal.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var cs = File.ReadAllText(archivoCs);

        // 1. ChangeTracker tipado
        Assert.Contains("ChangeTracker<EmpleadoSnapshot>", cs);
        Assert.Contains("_tracker.IsDirty(snapshotActual)", cs);

        // 2. Manejo honesto de fallas parciales (P-061)
        Assert.Contains("Los datos del empleado se actualizaron correctamente, pero no se pudo cambiar el estado", cs);
    }

    // =========================================================================
    // 6. INVARIANTES DE REPOSITORIO Y DESACOPLAMIENTO ATÓMICO (AP-03, ADR-026)
    // =========================================================================

    [Fact(DisplayName = "EmpleadoCrudRepository desacopla UpdateAsync de idEstado e invalida caché (AP-03, ADR-026)")]
    public void EmpleadoCrudRepository_DesacoplaUpdateDeEstado_E_InvalidaCache()
    {
        var archivoRepo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaDatos", "Repositories", "Empleados", "EmpleadoCrudRepository.cs");
        if (!File.Exists(archivoRepo)) return;

        var cs = File.ReadAllText(archivoRepo);

        // 1. UpdateAsync no sobreescribe idEstado (desacoplado atómicamente de CambiarEstadoAsync)
        int updateIndex = cs.IndexOf("public Task<Result> UpdateAsync");
        int nextMethodIndex = cs.IndexOf("public Task<Result> CambiarEstadoAsync");
        Assert.True(updateIndex >= 0 && nextMethodIndex > updateIndex);
        string updateBody = cs.Substring(updateIndex, nextMethodIndex - updateIndex);

        Assert.DoesNotContain("idEstado", updateBody);

        // 2. Invalida cache de la tabla empleados
        Assert.Contains("TagsCache.DeTabla(TagsCache.TablaEmpleados)", cs);
    }
}
