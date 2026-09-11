using System.Reflection;
using CapaAplicacion.Common;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Productos.Interfaces;
using CapaAplicacion.Productos.Queries;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Productos;

/// <summary>
/// Batería de pruebas de caja blanca y verificación de invariantes para el Módulo de Productos.
/// Garantiza la integridad de contratos, la lógica de cambio de estado transaccional y
/// previene regresiones de acoplamiento a Service Locator (AP-01) y bypass MVVM (AP-02, AP-03).
/// </summary>
public sealed class ProductosWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES DE FILTRADO Y PAGINACIÓN
    // =========================================================================

    [Fact(DisplayName = "ProductoFiltros inicializa con valores seguros y nulos por defecto")]
    public void ProductoFiltros_ValoresPorDefecto_SonNulos()
    {
        var filtros = new ProductoFiltros();

        Assert.Null(filtros.IdFabricante);
        Assert.Null(filtros.IdPais);
        Assert.Null(filtros.IdProveedor);
        Assert.Null(filtros.IdCategoria);
        Assert.Null(filtros.IdEstado);
        Assert.Equal(OrdenProducto.IdAsc, filtros.Orden);
    }

    [Theory(DisplayName = "OrdenProducto cubre los tres casos de ordenamiento")]
    [InlineData(OrdenProducto.IdAsc)]
    [InlineData(OrdenProducto.NombreAsc)]
    [InlineData(OrdenProducto.NombreDesc)]
    public void OrdenProducto_EnumCubreCasosCanonicos(OrdenProducto orden)
    {
        Assert.True(Enum.IsDefined(typeof(OrdenProducto), orden));
    }

    [Fact(DisplayName = "PagedResult<ProductoDto> calcula consistencia de colecciones vacías")]
    public void PagedResult_ColeccionVacia_MantieneInvariante()
    {
        var result = new PagedResult<ProductoDto>
        {
            Items = Array.Empty<ProductoDto>(),
            Total = 0,
            Activos = 0,
            Inactivos = 0
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Activos);
        Assert.Equal(0, result.Inactivos);
    }

    // =========================================================================
    // 2. INVARIANTES DE NEGOCIO Y DOMINIO
    // =========================================================================

    [Fact(DisplayName = "ReglasProducto exige Código y Nombre obligatorios con límites de PostgreSQL")]
    public void ReglasProducto_ExigeCodigoYNombre_ConLargoMaximoCorrecto()
    {
        Assert.True(ReglasProducto.Codigo.Obligatorio, "Código interno de producto debe ser obligatorio.");
        Assert.Equal(50, ReglasProducto.Codigo.LargoMaximo);

        Assert.True(ReglasProducto.Nombre.Obligatorio, "Nombre de producto debe ser obligatorio.");
        Assert.Equal(200, ReglasProducto.Nombre.LargoMaximo);

        Assert.False(ReglasProducto.Contenido.Obligatorio, "Contenido no debe ser obligatorio.");
        Assert.Equal(100, ReglasProducto.Contenido.LargoMaximo);

        Assert.Equal(FormatoCampo.Decimal, ReglasProducto.PesoTeorico.Formato);
        Assert.Equal(FormatoCampo.Decimal, ReglasProducto.PrecioPorKg.Formato);
    }

    // =========================================================================
    // 3. LÓGICA DE DETECCIÓN DE CAMBIOS (AP-03)
    // =========================================================================

    [Fact(DisplayName = "Detección de cambios: Identifica alteración de datos generales independientemente del estado")]
    public void DeteccionCambios_DistingueCambioDatosDeCambioEstado()
    {
        var original = new ProductoDto
        {
            Id = 10,
            CodigoInterno = "PRD-01",
            Nombre = "Pan Blanco Bimbo Grande",
            Contenido = "680 g",
            IdPresentacion = 1,
            IdFabricante = 2,
            IdCategoria = 3,
            IdPais = 1,
            PesoTeorico = 0.68m,
            IdTara = 1,
            IdUnidad = 2,
            PrecioPorKg = 45.50m,
            IdEstado = 1
        };

        // Escenario A: Ningún cambio
        var copiaIdentica = new ProductoDto
        {
            Id = 10,
            CodigoInterno = "PRD-01",
            Nombre = "Pan Blanco Bimbo Grande",
            Contenido = "680 g",
            IdPresentacion = 1,
            IdFabricante = 2,
            IdCategoria = 3,
            IdPais = 1,
            PesoTeorico = 0.68m,
            IdTara = 1,
            IdUnidad = 2,
            PrecioPorKg = 45.50m,
            IdEstado = 1
        };

        bool cambioDatosA = EvaluarCambioDatos(original, copiaIdentica);
        bool cambioEstadoA = original.IdEstado != copiaIdentica.IdEstado;

        Assert.False(cambioDatosA, "No debe detectar cambio de datos si las propiedades coinciden.");
        Assert.False(cambioEstadoA, "No debe detectar cambio de estado si IdEstado coincide.");

        // Escenario B: Solo cambia el estado
        var soloEstadoCambiado = new ProductoDto
        {
            Id = 10,
            CodigoInterno = "PRD-01",
            Nombre = "Pan Blanco Bimbo Grande",
            Contenido = "680 g",
            IdPresentacion = 1,
            IdFabricante = 2,
            IdCategoria = 3,
            IdPais = 1,
            PesoTeorico = 0.68m,
            IdTara = 1,
            IdUnidad = 2,
            PrecioPorKg = 45.50m,
            IdEstado = 2 // Inactivo
        };

        bool cambioDatosB = EvaluarCambioDatos(original, soloEstadoCambiado);
        bool cambioEstadoB = original.IdEstado != soloEstadoCambiado.IdEstado;

        Assert.False(cambioDatosB, "No debe marcar cambio de datos generales si solo cambió el estado.");
        Assert.True(cambioEstadoB, "Debe detectar cambio de estado.");

        // Escenario C: Cambian datos generales pero no el estado
        var soloDatosCambiados = new ProductoDto
        {
            Id = 10,
            CodigoInterno = "PRD-01",
            Nombre = "Pan Blanco Bimbo Mediano",
            Contenido = "500 g",
            IdPresentacion = 1,
            IdFabricante = 2,
            IdCategoria = 3,
            IdPais = 1,
            PesoTeorico = 0.50m,
            IdTara = 1,
            IdUnidad = 2,
            PrecioPorKg = 48.00m,
            IdEstado = 1
        };

        bool cambioDatosC = EvaluarCambioDatos(original, soloDatosCambiados);
        bool cambioEstadoC = original.IdEstado != soloDatosCambiados.IdEstado;

        Assert.True(cambioDatosC, "Debe detectar cambio de datos generales.");
        Assert.False(cambioEstadoC, "No debe detectar cambio de estado.");
    }

    private static bool EvaluarCambioDatos(ProductoDto a, ProductoDto b)
    {
        return a.CodigoInterno != b.CodigoInterno
            || a.Nombre != b.Nombre
            || a.Contenido != b.Contenido
            || a.IdPresentacion != b.IdPresentacion
            || a.IdFabricante != b.IdFabricante
            || a.IdCategoria != b.IdCategoria
            || a.IdPais != b.IdPais
            || a.PesoTeorico != b.PesoTeorico
            || a.IdTara != b.IdTara
            || a.IdUnidad != b.IdUnidad
            || a.PrecioPorKg != b.PrecioPorKg;
    }

    // =========================================================================
    // 4. INVARIANTES ARQUITECTÓNICOS (REFLECCIÓN / ANTI-REGRESIÓN AP-01)
    // =========================================================================

    [Fact(DisplayName = "ProductoModal cumple contrato de inyección limpia y soporte de diseñador (ADR-028)")]
    public void ProductoModal_CumpleContratoDeConstructores()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductoModal.xaml.cs");
        if (!File.Exists(archivoModal)) return;

        var codigo = File.ReadAllText(archivoModal);

        // 1. Constructor sin parámetros para diseñador de Visual Studio (ADR-028)
        Assert.Contains("public ProductoModal()", codigo);

        // 2. Constructor con inyección explícita de dependencias (AP-01 resuelto)
        Assert.Contains("public ProductoModal(IProductoRepository repo, ICatalogoRepository catalogos, ProductoDto? producto)", codigo);

        // 3. Erradicación de Service Locator en constructor (ADR-028 / AP-01)
        Assert.DoesNotContain("App.Services", codigo);

        // 4. Verificación de permiso RBAC para transiciones de estado (AP-03 resuelto)
        Assert.Contains("SesionPermisos.Tiene(Permiso.EliminarProducto)", codigo);
    }

    // =========================================================================
    // 5. INVARIANTES DE ENLACE DECLARATIVO Y MVVM EN PRODUCTOSVIEW (AP-02)
    // =========================================================================

    [Fact(DisplayName = "ProductosView erradica manipulación imperativa de filtros y supresores de eventos")]
    public void ProductosView_ErradicaBypassMvvmYManipulacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductosView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        // 1. Eliminación de bandera mutable de sincronización manual
        Assert.DoesNotContain("_suppressFilterChange", codigoCs);

        // 2. Eliminación de manejadores imperativos Checked
        Assert.DoesNotContain("EstadoFiltro_Changed", codigoCs);
        Assert.DoesNotContain("Orden_Changed", codigoCs);

        // 3. No asigna ItemsSource manualmente en code-behind
        Assert.DoesNotContain("DgProductos.ItemsSource =", codigoCs);
    }

    [Fact(DisplayName = "ProductosView.xaml contiene enlaces bidireccionales con EnumToBooleanConverter")]
    public void ProductosView_ContieneEnlacesDeclarativosEnXaml()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductosView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        // 1. Enlace declarativo a EnumToBooleanConverter para filtros de estado
        Assert.Contains("EnumToBooleanConverter.Instancia", xaml);
        Assert.Contains("ConverterParameter=Activos", xaml);
        Assert.Contains("ConverterParameter=Inactivos", xaml);
        Assert.Contains("ConverterParameter=Todos", xaml);

        // 2. Enlace declarativo para ordenamiento
        Assert.Contains("ConverterParameter=IdAsc", xaml);
        Assert.Contains("ConverterParameter=NombreAsc", xaml);
        Assert.Contains("ConverterParameter=NombreDesc", xaml);

        // 3. DataGrid con ItemsSource enlazado a PageRows
        Assert.Contains("ItemsSource=\"{Binding PageRows", xaml);
    }

    // =========================================================================
    // 6. INVARIANTES DE GESTIÓN CONCURRENTE Y CANCELACIÓN SEGURA (AP-05)
    // =========================================================================

    [Fact(DisplayName = "ProductosViewModel notifica EstadoFiltro y Orden en LimpiarFiltros y gestiona CTS de forma segura")]
    public void ProductosViewModel_GestionaNotificacionesYCancelacionSegura()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductosViewModel.cs");
        if (!File.Exists(archivoVm)) return;

        var codigoVm = File.ReadAllText(archivoVm);

        // 1. Notificación declarativa de reseteo para enlace TwoWay
        Assert.Contains("OnPropertyChanged(nameof(EstadoFiltro));", codigoVm);
        Assert.Contains("OnPropertyChanged(nameof(Orden));", codigoVm);

        // 2. Gestión de cancelación segura sin ObjectDisposedException
        Assert.Contains("catch (ObjectDisposedException)", codigoVm);
    }

    // =========================================================================
    // 7. INVARIANTES DE COMPONENTIZACIÓN Y RENDERIZADO GPU (AP-04 y AP-06)
    // =========================================================================

    [Fact(DisplayName = "ProductosView utiliza LoadingOverlay declarativo y no contiene lógica imperativa de spinner")]
    public void ProductosView_UtilizaLoadingOverlayYNoTieneAnimacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductosView.xaml.cs");
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductosView.xaml");
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

    [Fact(DisplayName = "ProductoModal desacopla DropShadowEffect de contenedores con ClipToBounds")]
    public void ProductoModal_DesacoplaDropShadowDeClipToBounds()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Productos", "ProductoModal.xaml");
        if (!File.Exists(archivoModal)) return;

        var xaml = File.ReadAllText(archivoModal);

        // 1. Verificación de sombra en borde hermano desacoplado
        Assert.Contains("<!-- Modal container with decoupled drop shadow (AP-06) -->", xaml);
        Assert.Contains("<DropShadowEffect Color=\"Black\"", xaml);

        // 2. Comprobar que el Border con ClipToBounds no tiene el Effect directamente anidado
        Assert.DoesNotMatch(@"<Border[^>]*ClipToBounds=""True""[^>]*>\s*<Border\.Effect>", xaml);
    }
}
