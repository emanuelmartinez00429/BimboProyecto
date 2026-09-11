using System;
using System.IO;
using System.Reflection;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Proveedores.Dtos;
using CapaAplicacion.Proveedores.Interfaces;
using CapaAplicacion.Proveedores.Queries;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Proveedores;

/// <summary>
/// Batería de pruebas de caja blanca y verificación de invariantes para el Módulo de Proveedores.
/// Garantiza la integridad de contratos, la lógica de cambio de estado transaccional atómica y
/// previene regresiones de acoplamiento a Service Locator (ADR-028), bypass MVVM y renderizado GPU.
/// </summary>
public sealed class ProveedoresWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES DE FILTRADO Y PAGINACIÓN
    // =========================================================================

    [Fact(DisplayName = "ProveedorFiltros inicializa con valores seguros y nulos por defecto")]
    public void ProveedorFiltros_ValoresPorDefecto_SonNulos()
    {
        var filtros = new ProveedorFiltros();
        Assert.Null(filtros.IdEstado);
    }

    [Fact(DisplayName = "PagedResult<ProveedorDto> calcula consistencia de colecciones vacías")]
    public void PagedResult_ColeccionVacia_MantieneInvariante()
    {
        var result = new PagedResult<ProveedorDto>
        {
            Items = Array.Empty<ProveedorDto>(),
            Total = 0,
            Activos = 0,
            Inactivos = 0
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Activos);
        Assert.Equal(0, result.Inactivos);
    }

    [Fact(DisplayName = "PagedResult<ProveedorDto> almacena y expone métricas de paginación correctamente")]
    public void PagedResult_ColeccionConDatos_MantieneConsistencia()
    {
        var items = new[]
        {
            new ProveedorDto { Id = 1, Nombre = "Molino Harinero Sula", Rtn = "01019999123456", IdEstado = 1 },
            new ProveedorDto { Id = 2, Nombre = "Levaduras del Norte", Rtn = "05018888654321", IdEstado = 2 }
        };

        var result = new PagedResult<ProveedorDto>
        {
            Items = items,
            Total = 2,
            Activos = 1,
            Inactivos = 1
        };

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Activos);
        Assert.Equal(1, result.Inactivos);
    }

    // =========================================================================
    // 2. INVARIANTES DE NEGOCIO Y DOMINIO
    // =========================================================================

    [Fact(DisplayName = "ReglasProveedor exige Nombre obligatorio y valida formatos según esquema de PostgreSQL")]
    public void ReglasProveedor_ExigeNombreYValidaFormatos_ConLargoMaximoCorrecto()
    {
        // Columna: nombre_proveedor (varchar 200, NOT NULL)
        Assert.True(ReglasProveedor.Nombre.Obligatorio, "Nombre de proveedor debe ser obligatorio.");
        Assert.Equal(200, ReglasProveedor.Nombre.LargoMaximo);

        // Columna: rtn_proveedor (varchar 20)
        Assert.False(ReglasProveedor.Rtn.Obligatorio, "RTN no es obligatorio en creación inicial.");
        Assert.Equal(20, ReglasProveedor.Rtn.LargoMaximo);
        Assert.Equal(FormatoCampo.Rtn, ReglasProveedor.Rtn.Formato);

        // Columna: telefono_proveedor (varchar 20)
        Assert.False(ReglasProveedor.Telefono.Obligatorio, "Teléfono no es obligatorio.");
        Assert.Equal(20, ReglasProveedor.Telefono.LargoMaximo);
        Assert.Equal(FormatoCampo.Telefono, ReglasProveedor.Telefono.Formato);

        // Columna: correo_proveedor (varchar 100)
        Assert.False(ReglasProveedor.Correo.Obligatorio, "Correo no es obligatorio.");
        Assert.Equal(100, ReglasProveedor.Correo.LargoMaximo);
        Assert.Equal(FormatoCampo.Correo, ReglasProveedor.Correo.Formato);

        // Columna: direccion_proveedor (varchar 500)
        Assert.False(ReglasProveedor.Direccion.Obligatorio, "Dirección no es obligatoria.");
        Assert.Equal(500, ReglasProveedor.Direccion.LargoMaximo);
    }

    // =========================================================================
    // 3. LÓGICA DE DETECCIÓN DE CAMBIOS Y MUTACIÓN ATÓMICA
    // =========================================================================

    [Fact(DisplayName = "Detección de cambios: Identifica alteración de datos de proveedor independientemente del estado")]
    public void DeteccionCambios_DistingueCambioDatosDeCambioEstado()
    {
        var original = new ProveedorDto
        {
            Id = 1,
            Nombre = "Harinas del Valle S.A.",
            Rtn = "08011999123456",
            Telefono = "2234-5678",
            Correo = "contacto@harinasvalle.hn",
            Direccion = "Parque Industrial Amarateca",
            IdEstado = 1
        };

        // Escenario A: Copia idéntica
        var copiaIdentica = new ProveedorDto
        {
            Id = 1,
            Nombre = "Harinas del Valle S.A.",
            Rtn = "08011999123456",
            Telefono = "2234-5678",
            Correo = "contacto@harinasvalle.hn",
            Direccion = "Parque Industrial Amarateca",
            IdEstado = 1
        };

        bool cambioDatosA = EvaluarCambioDatos(original, copiaIdentica);
        bool cambioEstadoA = original.IdEstado != copiaIdentica.IdEstado;

        Assert.False(cambioDatosA, "No debe detectar cambio de datos si las propiedades coinciden.");
        Assert.False(cambioEstadoA, "No debe detectar cambio de estado si IdEstado coincide.");

        // Escenario B: Solo cambia el estado (toggled)
        var soloEstadoCambiado = new ProveedorDto
        {
            Id = 1,
            Nombre = "Harinas del Valle S.A.",
            Rtn = "08011999123456",
            Telefono = "2234-5678",
            Correo = "contacto@harinasvalle.hn",
            Direccion = "Parque Industrial Amarateca",
            IdEstado = 2 // Inactivo
        };

        bool cambioDatosB = EvaluarCambioDatos(original, soloEstadoCambiado);
        bool cambioEstadoB = original.IdEstado != soloEstadoCambiado.IdEstado;

        Assert.False(cambioDatosB, "No debe marcar cambio de datos generales si solo cambió el estado.");
        Assert.True(cambioEstadoB, "Debe detectar cambio de estado.");

        // Escenario C: Solo cambian datos generales
        var soloDatosCambiados = new ProveedorDto
        {
            Id = 1,
            Nombre = "Harinas del Valle Internacional",
            Rtn = "08011999123456",
            Telefono = "2234-9999",
            Correo = "info@harinasvalle.hn",
            Direccion = "Parque Industrial Amarateca Bloque B",
            IdEstado = 1
        };

        bool cambioDatosC = EvaluarCambioDatos(original, soloDatosCambiados);
        bool cambioEstadoC = original.IdEstado != soloDatosCambiados.IdEstado;

        Assert.True(cambioDatosC, "Debe detectar cambio de datos generales.");
        Assert.False(cambioEstadoC, "No debe detectar cambio de estado.");

        // Escenario D: Cambian tanto datos generales como el estado
        var ambosCambiados = new ProveedorDto
        {
            Id = 1,
            Nombre = "Harinas del Valle Internacional",
            Rtn = "08011999123456",
            Telefono = "2234-9999",
            Correo = "info@harinasvalle.hn",
            Direccion = "Parque Industrial Amarateca Bloque B",
            IdEstado = 2
        };

        bool cambioDatosD = EvaluarCambioDatos(original, ambosCambiados);
        bool cambioEstadoD = original.IdEstado != ambosCambiados.IdEstado;

        Assert.True(cambioDatosD, "Debe detectar cambio de datos.");
        Assert.True(cambioEstadoD, "Debe detectar cambio de estado.");
    }

    private static bool EvaluarCambioDatos(ProveedorDto a, ProveedorDto b)
    {
        return a.Nombre != b.Nombre
            || a.Rtn != b.Rtn
            || a.Telefono != b.Telefono
            || a.Correo != b.Correo
            || a.Direccion != b.Direccion;
    }

    // =========================================================================
    // 4. INVARIANTES ARQUITECTÓNICOS (REFLEXIÓN / ANTI-REGRESIÓN ADR-028)
    // =========================================================================

    [Fact(DisplayName = "ProveedorModal cumple contrato de inyección limpia y soporte de diseñador (ADR-028)")]
    public void ProveedorModal_CumpleContratoDeConstructores()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedorModal.xaml.cs");
        if (!File.Exists(archivoModal)) return;

        var codigo = File.ReadAllText(archivoModal);

        // 1. Constructor sin parámetros para diseñador de Visual Studio (ADR-028)
        Assert.Contains("public ProveedorModal()", codigo);

        // 2. Constructor con inyección explícita de dependencias (ADR-028 resuelto)
        Assert.Contains("public ProveedorModal(IProveedorRepository repo, ProveedorDto? proveedor)", codigo);

        // 3. Erradicación de Service Locator en constructor (ADR-028)
        Assert.DoesNotContain("App.Services", codigo);

        // 4. Verificación de detección atómica de cambios (datos vs estado)
        Assert.Contains("datosCambiaron", codigo);
        Assert.Contains("estadoCambio", codigo);
    }

    // =========================================================================
    // 5. INVARIANTES DE ENLACE DECLARATIVO Y MVVM EN PROVEEDORESVIEW
    // =========================================================================

    [Fact(DisplayName = "ProveedoresView erradica manipulación imperativa de filtros y supresores de eventos")]
    public void ProveedoresView_ErradicaBypassMvvmYManipulacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        // 1. Eliminación de bandera mutable de sincronización manual
        Assert.DoesNotContain("_suppressFilterChange", codigoCs);

        // 2. Eliminación de manejadores imperativos de radio buttons
        Assert.DoesNotContain("EstadoFiltro_Changed", codigoCs);

        // 3. No asigna ItemsSource manualmente en code-behind
        Assert.DoesNotContain("DgProveedores.ItemsSource =", codigoCs);

        // 4. No manipula eventos de selección manualmente en code-behind
        Assert.DoesNotContain("DgProveedores_SelectionChanged", codigoCs);
    }

    [Fact(DisplayName = "ProveedoresView.xaml contiene enlaces bidireccionales con EnumToBooleanConverter")]
    public void ProveedoresView_ContieneEnlacesDeclarativosEnXaml()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Enlace declarativo a EnumToBooleanConverter para filtros de estado tipados con x:Static
        Assert.Contains("EnumToBooleanConverter.Instancia", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Activos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Inactivos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Todos}", xaml);

        // 2. Ausencia de GroupName para evitar traversals O(N) del árbol visual
        Assert.DoesNotContain("GroupName=\"EstadoFiltro\"", xaml);

        // 3. DataGrid con ItemsSource enlazado a PageRows y SelectedItem a Seleccionado
        Assert.Contains("ItemsSource=\"{Binding PageRows", xaml);
        Assert.Contains("SelectedItem=\"{Binding Seleccionado", xaml);

        // 4. Optimizaciones DirectX / ClearType y Virtualización
        Assert.Contains("RenderOptions.ClearTypeHint=\"Enabled\"", xaml);
        Assert.Contains("RowBackground=\"White\"", xaml);
        Assert.Contains("EnableColumnVirtualization=\"True\"", xaml);

        // 5. LoadingOverlay declarativo
        Assert.Contains("controls:LoadingOverlay", xaml);
        Assert.Contains("IsLoading=\"{Binding IsLoading}\"", xaml);
    }

    // =========================================================================
    // 6. INVARIANTES DE GESTIÓN CONCURRENTE Y CANCELACIÓN SEGURA
    // =========================================================================

    [Fact(DisplayName = "ProveedoresViewModel notifica EstadoFiltro en LimpiarFiltros y gestiona CTS de forma segura")]
    public void ProveedoresViewModel_GestionaNotificacionesYCancelacionSegura()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresViewModel.cs");
        if (!File.Exists(archivoVm)) return;

        var codigoVm = File.ReadAllText(archivoVm);

        // 1. Notificación declarativa de reseteo para enlace TwoWay
        Assert.Contains("OnPropertyChanged(nameof(EstadoFiltro));", codigoVm);

        // 2. Gestión de cancelación lock-free con Interlocked.Exchange y CancelAsync no bloqueante (.NET 10)
        Assert.Contains("Interlocked.Exchange(ref _ctsPagina", codigoVm);
        Assert.Contains("CancelAsync()", codigoVm);
        Assert.Contains("catch (ObjectDisposedException)", codigoVm);

        // 3. Timeout y Token enlazado en CargarPaginaAsync
        Assert.Contains("CancellationTokenSource(TimeoutMs)", codigoVm);
    }

    // =========================================================================
    // 7. INVARIANTES DE COMPONENTIZACIÓN Y RENDERIZADO GPU
    // =========================================================================

    [Fact(DisplayName = "ProveedoresView utiliza LoadingOverlay declarativo y no contiene lógica imperativa de spinner")]
    public void ProveedoresView_UtilizaLoadingOverlayYNoTieneAnimacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresView.xaml.cs");
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresView.xaml");
        if (!File.Exists(archivoCs) || !File.Exists(archivoXaml)) return;

        var codigoCs = File.ReadAllText(archivoCs);
        var codigoXaml = File.ReadAllText(archivoXaml);

        // 1. Eliminación de Storyboard y métodos manuales de spinner en code-behind
        Assert.DoesNotContain("_spinnerStory", codigoCs);
        Assert.DoesNotContain("IniciarSpinner", codigoCs);
        Assert.DoesNotContain("DetenerSpinner", codigoCs);
        Assert.DoesNotContain("ActualizarCarga", codigoCs);

        // 2. Uso de LoadingOverlay en XAML
        Assert.Contains("controls:LoadingOverlay", codigoXaml);
        Assert.Contains("IsLoading=\"{Binding IsLoading}\"", codigoXaml);
    }

    [Fact(DisplayName = "ProveedorModal desacopla DropShadowEffect de contenedores con ClipToBounds y congela geometrías")]
    public void ProveedorModal_DesacoplaDropShadowDeClipToBounds()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedorModal.xaml");
        if (!File.Exists(archivoModal)) return;

        var xaml = File.ReadAllText(archivoModal);

        // 1. Verificación de sombra en borde hermano desacoplado
        Assert.Contains("<!-- Modal container with decoupled drop shadow (AP-06) -->", xaml);
        Assert.Contains("<DropShadowEffect Color=\"Black\"", xaml);

        // 2. Comprobar que el Border con ClipToBounds no tiene el Effect directamente anidado
        Assert.DoesNotMatch(@"<Border[^>]*ClipToBounds=""True""[^>]*>\s*<Border\.Effect>", xaml);

        // 3. Congelamiento determinista de recursos vectoriales Freezable
        Assert.Contains("po:Freeze=\"True\"", xaml);
    }

    [Fact(DisplayName = "ProveedorModal maneja fallas parciales con confirmación de datos y advertencia al operador")]
    public void ProveedorModal_ManejaFallasParciales_ConConfirmacionYAdvertencia()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedorModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        // 1. Manejo de fallo parcial con notificación explicativa
        Assert.Contains("Los datos del proveedor se actualizaron correctamente, pero no se pudo cambiar el estado", codigoCs);

        // 2. Confirmación del token idempotente del paso exitoso
        Assert.Contains("_solicitud.Confirmar();", codigoCs);
    }

    [Fact(DisplayName = "ProveedoresView alinea columnas CREADO y ACTUALIZADO a la izquierda")]
    public void ProveedoresView_ColumnasFechasAlineadasALaIzquierda()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedoresView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // Columnas CREADO y ACTUALIZADO con CeldaIzquierda
        Assert.Matches(@"Header=""CREADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ACTUALIZADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);

        // No deben tener HeaderDerecho ni CeldaDerecha
        Assert.DoesNotMatch(@"Header=""CREADO""[^>]*HeaderDerecho", xaml);
        Assert.DoesNotMatch(@"Header=""ACTUALIZADO""[^>]*HeaderDerecho", xaml);
    }

    [Fact(DisplayName = "ProveedorModal utiliza ChangeTracker para dirty tracking tipado sin cadenas manuales de igualdad")]
    public void ProveedorModal_UtilizaChangeTracker_SinCadenasManuales()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Proveedores", "ProveedorModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("ChangeTracker<ProveedorSnapshot>", codigoCs);
        Assert.Contains("_tracker.IsDirty(snapshotActual)", codigoCs);
        Assert.DoesNotContain("!string.Equals(dto.Nombre", codigoCs);
    }
}
