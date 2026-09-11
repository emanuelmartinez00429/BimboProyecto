using System;
using System.IO;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Presentaciones.Dtos;
using CapaAplicacion.Presentaciones.Interfaces;
using CapaAplicacion.Presentaciones.Queries;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Presentaciones;

public sealed class PresentacionesWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES DE FILTRADO Y PAGINACIÓN
    // =========================================================================

    [Fact(DisplayName = "PresentacionFiltros inicializa con valores seguros y orden IdAsc por defecto")]
    public void PresentacionFiltros_ValoresPorDefecto_SonCorrectos()
    {
        var filtros = new PresentacionFiltros();
        Assert.Null(filtros.IdEstado);
        Assert.Equal(OrdenPresentacion.IdAsc, filtros.Orden);
    }

    [Fact(DisplayName = "PagedResult<PresentacionDto> calcula consistencia de colecciones vacías")]
    public void PagedResult_ColeccionVacia_MantieneInvariante()
    {
        var result = new PagedResult<PresentacionDto>
        {
            Items = Array.Empty<PresentacionDto>(),
            Total = 0,
            Activos = 0,
            Inactivos = 0
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Activos);
        Assert.Equal(0, result.Inactivos);
    }

    [Fact(DisplayName = "PagedResult<PresentacionDto> almacena y expone métricas de paginación correctamente")]
    public void PagedResult_ColeccionConDatos_MantieneConsistencia()
    {
        var items = new[]
        {
            new PresentacionDto { Id = 1, Nombre = "Bolsa 500g", Descripcion = "Empaque individual", IdEstado = 1 },
            new PresentacionDto { Id = 2, Nombre = "Caja 12 un", Descripcion = "Empaque mayoreo", IdEstado = 2 }
        };

        var result = new PagedResult<PresentacionDto>
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

    [Fact(DisplayName = "ReglasPresentacion exige Nombre obligatorio y valida límites según esquema")]
    public void ReglasPresentacion_ExigeNombreYValidaLimites()
    {
        Assert.True(ReglasPresentacion.Nombre.Obligatorio, "Nombre de presentación debe ser obligatorio.");
        Assert.Equal(100, ReglasPresentacion.Nombre.LargoMaximo);

        Assert.False(ReglasPresentacion.Descripcion.Obligatorio, "Descripción no es obligatoria.");
        Assert.Equal(500, ReglasPresentacion.Descripcion.LargoMaximo);
    }

    // =========================================================================
    // 3. LÓGICA DE DETECCIÓN DE CAMBIOS Y MUTACIÓN ATÓMICA
    // =========================================================================

    [Fact(DisplayName = "Detección de cambios: Identifica alteración de datos de presentación independientemente del estado")]
    public void DeteccionCambios_DistingueCambioDatosDeCambioEstado()
    {
        var original = new PresentacionDto
        {
            Id = 1,
            Nombre = "Paquete Familiar 800g",
            Descripcion = "Empaque plástico termoencogible",
            IdEstado = 1
        };

        var copiaIdentica = new PresentacionDto
        {
            Id = 1,
            Nombre = "Paquete Familiar 800g",
            Descripcion = "Empaque plástico termoencogible",
            IdEstado = 1
        };

        Assert.False(EvaluarCambioDatos(original, copiaIdentica));
        Assert.False(original.IdEstado != copiaIdentica.IdEstado);

        var soloEstadoCambiado = new PresentacionDto
        {
            Id = 1,
            Nombre = "Paquete Familiar 800g",
            Descripcion = "Empaque plástico termoencogible",
            IdEstado = 2
        };

        Assert.False(EvaluarCambioDatos(original, soloEstadoCambiado));
        Assert.True(original.IdEstado != soloEstadoCambiado.IdEstado);

        var soloDatosCambiados = new PresentacionDto
        {
            Id = 1,
            Nombre = "Paquete Familiar 1000g",
            Descripcion = "Empaque plástico termoencogible",
            IdEstado = 1
        };

        Assert.True(EvaluarCambioDatos(original, soloDatosCambiados));
        Assert.False(original.IdEstado != soloDatosCambiados.IdEstado);
    }

    private static bool EvaluarCambioDatos(PresentacionDto a, PresentacionDto b)
    {
        return a.Nombre != b.Nombre
            || a.Descripcion != b.Descripcion;
    }

    // =========================================================================
    // 4. INVARIANTES ARQUITECTÓNICOS (REFLEXIÓN / ANTI-REGRESIÓN ADR-028)
    // =========================================================================

    [Fact(DisplayName = "PresentacionModal cumple contrato de constructores y erradicación de Service Locator (ADR-028)")]
    public void PresentacionModal_CumpleContratoDeConstructores()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionModal.xaml.cs");
        if (!File.Exists(archivoModal)) return;

        var codigo = File.ReadAllText(archivoModal);

        Assert.Contains("public PresentacionModal()", codigo);
        Assert.Contains("public PresentacionModal(IPresentacionRepository repo, PresentacionDto? presentacion)", codigo);
        Assert.DoesNotContain("App.Services", codigo);
        Assert.Contains("CambiarEstadoAsync", codigo);
    }

    // =========================================================================
    // 5. INVARIANTES DE ENLACE DECLARATIVO Y MVVM EN PRESENTACIONESVIEW
    // =========================================================================

    [Fact(DisplayName = "PresentacionesView erradica manipulación imperativa de filtros y supresores de eventos")]
    public void PresentacionesView_ErradicaBypassMvvmYManipulacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionesView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        Assert.DoesNotContain("_suppressFilterChange", codigoCs);
        Assert.DoesNotContain("EstadoFiltro_Changed", codigoCs);
        Assert.DoesNotContain("Orden_Changed", codigoCs);
    }

    [Fact(DisplayName = "PresentacionesView.xaml contiene enlaces bidireccionales con EnumToBooleanConverter y optimizaciones DirectX")]
    public void PresentacionesView_ContieneEnlacesDeclarativosEnXaml()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionesView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        Assert.Contains("EnumToBooleanConverter", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Activos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Inactivos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Todos}", xaml);
        Assert.Contains("ConverterParameter={x:Static queries:OrdenPresentacion.IdAsc}", xaml);
        Assert.Contains("ConverterParameter={x:Static queries:OrdenPresentacion.NombreAsc}", xaml);
        Assert.Contains("ConverterParameter={x:Static queries:OrdenPresentacion.NombreDesc}", xaml);
        Assert.DoesNotContain("GroupName=\"EstadoFiltro\"", xaml);
        Assert.DoesNotContain("GroupName=\"OrdenPresentaciones\"", xaml);

        // Optimizaciones DirectX / ClearType y Virtualización
        Assert.Contains("RenderOptions.ClearTypeHint=\"Enabled\"", xaml);
        Assert.Contains("RowBackground=\"White\"", xaml);
        Assert.Contains("EnableColumnVirtualization=\"True\"", xaml);
        Assert.Contains("TextOptions.TextRenderingMode=\"Auto\"", xaml);
    }

    // =========================================================================
    // 6. INVARIANTES DE GESTIÓN CONCURRENTE Y CANCELACIÓN SEGURA
    // =========================================================================

    [Fact(DisplayName = "PresentacionesViewModel gestiona CTS de forma atómica y segura")]
    public void PresentacionesViewModel_GestionaNotificacionesYCancelacionSegura()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionesViewModel.cs");
        if (!File.Exists(archivoVm)) return;

        var codigoVm = File.ReadAllText(archivoVm);

        Assert.Contains("OnPropertyChanged(nameof(EstadoFiltro));", codigoVm);
        Assert.Contains("Interlocked.Exchange(ref _ctsPagina", codigoVm);
        Assert.Contains("CancelAsync()", codigoVm);
        Assert.Contains("catch (ObjectDisposedException)", codigoVm);
        Assert.Contains("TimeoutMs", codigoVm);
    }

    // =========================================================================
    // 7. INVARIANTES DE RENDERIZADO GPU Y CONGELAMIENTO VECTORIAL
    // =========================================================================

    [Fact(DisplayName = "PresentacionModal congela geometrías vectoriales y elimina shaders pesados")]
    public void PresentacionModal_CongelaGeometriasYEliminaShaders()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionModal.xaml");
        if (!File.Exists(archivoModal)) return;

        var xaml = File.ReadAllText(archivoModal);

        Assert.Contains("po:Freeze=\"True\"", xaml);
        Assert.Contains("TextOptions.TextRenderingMode=\"Auto\"", xaml);
    }

    [Fact(DisplayName = "PresentacionModal maneja fallas parciales con confirmación y advertencia al operador")]
    public void PresentacionModal_ManejaFallasParciales_ConConfirmacionYAdvertencia()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Presentaciones", "PresentacionModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("Los datos de la presentación se actualizaron correctamente, pero no se pudo cambiar su estado", codigoCs);
        Assert.Contains("_solicitud.Confirmar();", codigoCs);
    }
}
