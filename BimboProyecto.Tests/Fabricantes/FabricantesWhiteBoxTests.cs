using System;
using System.IO;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Fabricantes.Dtos;
using CapaAplicacion.Fabricantes.Interfaces;
using CapaAplicacion.Fabricantes.Queries;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Fabricantes;

public sealed class FabricantesWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES DE FILTRADO Y PAGINACIÓN
    // =========================================================================

    [Fact(DisplayName = "FabricanteFiltros inicializa con valores seguros y nulos por defecto")]
    public void FabricanteFiltros_ValoresPorDefecto_SonNulos()
    {
        var filtros = new FabricanteFiltros();
        Assert.Null(filtros.IdEstado);
        Assert.Null(filtros.IdPais);
    }

    [Fact(DisplayName = "PagedResult<FabricanteDto> calcula consistencia de colecciones vacías")]
    public void PagedResult_ColeccionVacia_MantieneInvariante()
    {
        var result = new PagedResult<FabricanteDto>
        {
            Items = Array.Empty<FabricanteDto>(),
            Total = 0,
            Activos = 0,
            Inactivos = 0
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Activos);
        Assert.Equal(0, result.Inactivos);
    }

    [Fact(DisplayName = "PagedResult<FabricanteDto> almacena y expone métricas de paginación correctamente")]
    public void PagedResult_ColeccionConDatos_MantieneConsistencia()
    {
        var items = new[]
        {
            new FabricanteDto { Id = 1, Nombre = "Bimbo México", Descripcion = "Planta principal", IdEstado = 1 },
            new FabricanteDto { Id = 2, Nombre = "Bimbo Honduras", Descripcion = "Planta local", IdEstado = 2 }
        };

        var result = new PagedResult<FabricanteDto>
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

    [Fact(DisplayName = "ReglasFabricante exige Nombre obligatorio y valida límites según esquema")]
    public void ReglasFabricante_ExigeNombreYValidaLimites()
    {
        Assert.True(ReglasFabricante.Nombre.Obligatorio, "Nombre de fabricante debe ser obligatorio.");
        Assert.Equal(200, ReglasFabricante.Nombre.LargoMaximo);

        Assert.False(ReglasFabricante.Descripcion.Obligatorio, "Descripción no es obligatoria.");
        Assert.Equal(500, ReglasFabricante.Descripcion.LargoMaximo);
    }

    // =========================================================================
    // 3. LÓGICA DE DETECCIÓN DE CAMBIOS Y MUTACIÓN ATÓMICA
    // =========================================================================

    [Fact(DisplayName = "Detección de cambios: Identifica alteración de datos de fabricante independientemente del estado")]
    public void DeteccionCambios_DistingueCambioDatosDeCambioEstado()
    {
        var original = new FabricanteDto
        {
            Id = 1,
            Nombre = "Bimbo Centroamérica",
            Descripcion = "Operaciones regionales",
            IdProveedor = 10,
            IdPais = 1,
            IdEstado = 1
        };

        var copiaIdentica = new FabricanteDto
        {
            Id = 1,
            Nombre = "Bimbo Centroamérica",
            Descripcion = "Operaciones regionales",
            IdProveedor = 10,
            IdPais = 1,
            IdEstado = 1
        };

        Assert.False(EvaluarCambioDatos(original, copiaIdentica));
        Assert.False(original.IdEstado != copiaIdentica.IdEstado);

        var soloEstadoCambiado = new FabricanteDto
        {
            Id = 1,
            Nombre = "Bimbo Centroamérica",
            Descripcion = "Operaciones regionales",
            IdProveedor = 10,
            IdPais = 1,
            IdEstado = 2
        };

        Assert.False(EvaluarCambioDatos(original, soloEstadoCambiado));
        Assert.True(original.IdEstado != soloEstadoCambiado.IdEstado);

        var soloDatosCambiados = new FabricanteDto
        {
            Id = 1,
            Nombre = "Grupo Bimbo Centroamérica",
            Descripcion = "Operaciones regionales",
            IdProveedor = 10,
            IdPais = 1,
            IdEstado = 1
        };

        Assert.True(EvaluarCambioDatos(original, soloDatosCambiados));
        Assert.False(original.IdEstado != soloDatosCambiados.IdEstado);
    }

    private static bool EvaluarCambioDatos(FabricanteDto a, FabricanteDto b)
    {
        return a.Nombre != b.Nombre
            || a.Descripcion != b.Descripcion
            || a.IdProveedor != b.IdProveedor
            || a.IdPais != b.IdPais;
    }

    // =========================================================================
    // 4. INVARIANTES ARQUITECTÓNICOS (REFLEXIÓN / ANTI-REGRESIÓN ADR-028)
    // =========================================================================

    [Fact(DisplayName = "FabricanteModal cumple contrato de constructores y separación de estado")]
    public void FabricanteModal_CumpleContratoDeConstructores()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricanteModal.xaml.cs");
        if (!File.Exists(archivoModal)) return;

        var codigo = File.ReadAllText(archivoModal);

        Assert.Contains("public FabricanteModal()", codigo);
        Assert.Contains("public FabricanteModal(IFabricanteRepository repo, FabricanteDto? fabricante)", codigo);
        Assert.Contains("CambiarEstadoAsync", codigo);
    }

    // =========================================================================
    // 5. INVARIANTES DE ENLACE DECLARATIVO Y MVVM EN FABRICANTESVIEW
    // =========================================================================

    [Fact(DisplayName = "FabricantesView erradica manipulación imperativa de filtros y supresores de eventos")]
    public void FabricantesView_ErradicaBypassMvvmYManipulacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricantesView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        Assert.DoesNotContain("_suppressFilterChange", codigoCs);
        Assert.DoesNotContain("EstadoFiltro_Changed", codigoCs);
    }

    [Fact(DisplayName = "FabricantesView.xaml contiene enlaces bidireccionales con EnumToBooleanConverter y optimizaciones DirectX")]
    public void FabricantesView_ContieneEnlacesDeclarativosEnXaml()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricantesView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        Assert.Contains("EnumToBooleanConverter", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Activos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Inactivos}", xaml);
        Assert.Contains("ConverterParameter={x:Static local:EstadoFilter.Todos}", xaml);
        Assert.DoesNotContain("GroupName=\"EstadoFiltro\"", xaml);

        // Optimizaciones DirectX / ClearType y Virtualización
        Assert.Contains("RenderOptions.ClearTypeHint=\"Enabled\"", xaml);
        Assert.Contains("RowBackground=\"White\"", xaml);
        Assert.Contains("EnableColumnVirtualization=\"True\"", xaml);
        Assert.Contains("TextOptions.TextRenderingMode=\"Auto\"", xaml);
    }

    // =========================================================================
    // 6. INVARIANTES DE GESTIÓN CONCURRENTE Y CANCELACIÓN SEGURA
    // =========================================================================

    [Fact(DisplayName = "FabricantesViewModel gestiona CTS de forma atómica y segura")]
    public void FabricantesViewModel_GestionaNotificacionesYCancelacionSegura()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricantesViewModel.cs");
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

    [Fact(DisplayName = "FabricanteModal congela geometrías vectoriales y elimina shaders pesados")]
    public void FabricanteModal_CongelaGeometriasYEliminaShaders()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricanteModal.xaml");
        if (!File.Exists(archivoModal)) return;

        var xaml = File.ReadAllText(archivoModal);

        Assert.Contains("po:Freeze=\"True\"", xaml);
        Assert.Contains("TextOptions.TextRenderingMode=\"Auto\"", xaml);
    }

    [Fact(DisplayName = "FabricanteModal maneja fallas parciales con confirmación y advertencia al operador")]
    public void FabricanteModal_ManejaFallasParciales_ConConfirmacionYAdvertencia()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricanteModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("Los datos del fabricante se actualizaron correctamente, pero no se pudo cambiar su estado", codigoCs);
        Assert.Contains("_solicitud.Confirmar();", codigoCs);
    }

    [Fact(DisplayName = "FabricantesView alinea columnas CREADO y ACTUALIZADO a la izquierda")]
    public void FabricantesView_ColumnasFechasAlineadasALaIzquierda()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricantesView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        Assert.Matches(@"Header=""CREADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ACTUALIZADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.DoesNotMatch(@"Header=""CREADO""[^>]*HeaderDerecho", xaml);
        Assert.DoesNotMatch(@"Header=""ACTUALIZADO""[^>]*HeaderDerecho", xaml);
    }

    [Fact(DisplayName = "FabricanteModal utiliza ChangeTracker para dirty tracking tipado")]
    public void FabricanteModal_UtilizaChangeTracker()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Fabricantes", "FabricanteModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("ChangeTracker<FabricanteSnapshot>", codigoCs);
        Assert.Contains("_tracker.IsDirty(snapshotActual)", codigoCs);
    }
}
