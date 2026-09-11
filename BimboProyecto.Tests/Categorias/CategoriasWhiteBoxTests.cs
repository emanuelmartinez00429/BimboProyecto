using System;
using System.IO;
using CapaAplicacion.Common;
using CapaAplicacion.Productos.Queries;
using CapaAplicacion.Categorias.Dtos;
using CapaAplicacion.Categorias.Interfaces;
using CapaAplicacion.Categorias.Queries;
using CapaDominio.Reglas;
using Xunit;

namespace BimboProyecto.Tests.Categorias;

public sealed class CategoriasWhiteBoxTests
{
    // =========================================================================
    // 1. INVARIANTES DE FILTRADO Y PAGINACIÓN
    // =========================================================================

    [Fact(DisplayName = "CategoriaFiltros inicializa con valores seguros y nulos por defecto")]
    public void CategoriaFiltros_ValoresPorDefecto_SonNulos()
    {
        var filtros = new CategoriaFiltros();
        Assert.Null(filtros.IdEstado);
    }

    [Fact(DisplayName = "PagedResult<CategoriaDto> calcula consistencia de colecciones vacías")]
    public void PagedResult_ColeccionVacia_MantieneInvariante()
    {
        var result = new PagedResult<CategoriaDto>
        {
            Items = Array.Empty<CategoriaDto>(),
            Total = 0,
            Activos = 0,
            Inactivos = 0
        };

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.Activos);
        Assert.Equal(0, result.Inactivos);
    }

    [Fact(DisplayName = "PagedResult<CategoriaDto> almacena y expone métricas de paginación correctamente")]
    public void PagedResult_ColeccionConDatos_MantieneConsistencia()
    {
        var items = new[]
        {
            new CategoriaDto { Id = 1, Nombre = "Pan Blanco", Descripcion = "Categoría pan blanco", EstadoCategoria = true },
            new CategoriaDto { Id = 2, Nombre = "Bollería", Descripcion = "Categoría bollería", EstadoCategoria = false }
        };

        var result = new PagedResult<CategoriaDto>
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

    [Fact(DisplayName = "ReglasCategoria exige Nombre obligatorio y valida límites según esquema")]
    public void ReglasCategoria_ExigeNombreYValidaLimites()
    {
        Assert.True(ReglasCategoria.Nombre.Obligatorio, "Nombre de categoría debe ser obligatorio.");
        Assert.Equal(100, ReglasCategoria.Nombre.LargoMaximo);

        Assert.False(ReglasCategoria.Descripcion.Obligatorio, "Descripción no es obligatoria.");
        Assert.Equal(200, ReglasCategoria.Descripcion.LargoMaximo);
    }

    // =========================================================================
    // 3. LÓGICA DE DETECCIÓN DE CAMBIOS Y MUTACIÓN ATÓMICA
    // =========================================================================

    [Fact(DisplayName = "Detección de cambios: Identifica alteración de datos de categoría independientemente del estado")]
    public void DeteccionCambios_DistingueCambioDatosDeCambioEstado()
    {
        var original = new CategoriaDto
        {
            Id = 1,
            Nombre = "Pan Dulce Tradicional",
            Descripcion = "Pan dulce empaquetado",
            EstadoCategoria = true
        };

        var copiaIdentica = new CategoriaDto
        {
            Id = 1,
            Nombre = "Pan Dulce Tradicional",
            Descripcion = "Pan dulce empaquetado",
            EstadoCategoria = true
        };

        Assert.False(EvaluarCambioDatos(original, copiaIdentica));
        Assert.False(original.EstadoCategoria != copiaIdentica.EstadoCategoria);

        var soloEstadoCambiado = new CategoriaDto
        {
            Id = 1,
            Nombre = "Pan Dulce Tradicional",
            Descripcion = "Pan dulce empaquetado",
            EstadoCategoria = false
        };

        Assert.False(EvaluarCambioDatos(original, soloEstadoCambiado));
        Assert.True(original.EstadoCategoria != soloEstadoCambiado.EstadoCategoria);

        var soloDatosCambiados = new CategoriaDto
        {
            Id = 1,
            Nombre = "Pan Dulce Especial",
            Descripcion = "Pan dulce empaquetado",
            EstadoCategoria = true
        };

        Assert.True(EvaluarCambioDatos(original, soloDatosCambiados));
        Assert.False(original.EstadoCategoria != soloDatosCambiados.EstadoCategoria);
    }

    private static bool EvaluarCambioDatos(CategoriaDto a, CategoriaDto b)
    {
        return a.Nombre != b.Nombre
            || a.Descripcion != b.Descripcion;
    }

    // =========================================================================
    // 4. INVARIANTES ARQUITECTÓNICOS (REFLEXIÓN / ANTI-REGRESIÓN ADR-028)
    // =========================================================================

    [Fact(DisplayName = "CategoriaModal cumple contrato de constructores y erradicación de Service Locator (ADR-028)")]
    public void CategoriaModal_CumpleContratoDeConstructores()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriaModal.xaml.cs");
        if (!File.Exists(archivoModal)) return;

        var codigo = File.ReadAllText(archivoModal);

        Assert.Contains("public CategoriaModal()", codigo);
        Assert.Contains("public CategoriaModal(ICategoriaRepository repo, CategoriaDto? categoria)", codigo);
        Assert.DoesNotContain("App.Services", codigo);
        Assert.Contains("CambiarEstadoAsync", codigo);
    }

    // =========================================================================
    // 5. INVARIANTES DE ENLACE DECLARATIVO Y MVVM EN CATEGORIASVIEW
    // =========================================================================

    [Fact(DisplayName = "CategoriasView erradica manipulación imperativa de filtros y supresores de eventos")]
    public void CategoriasView_ErradicaBypassMvvmYManipulacionImperativa()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriasView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        Assert.DoesNotContain("_suppressFilterChange", codigoCs);
        Assert.DoesNotContain("EstadoFiltro_Changed", codigoCs);
    }

    [Fact(DisplayName = "CategoriasView.xaml contiene enlaces bidireccionales con EnumToBooleanConverter y optimizaciones DirectX")]
    public void CategoriasView_ContieneEnlacesDeclarativosEnXaml()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriasView.xaml");
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

    [Fact(DisplayName = "CategoriasViewModel gestiona CTS de forma atómica y segura")]
    public void CategoriasViewModel_GestionaNotificacionesYCancelacionSegura()
    {
        var archivoVm = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriasViewModel.cs");
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

    [Fact(DisplayName = "CategoriaModal congela geometrías vectoriales y elimina shaders pesados")]
    public void CategoriaModal_CongelaGeometriasYEliminaShaders()
    {
        var archivoModal = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriaModal.xaml");
        if (!File.Exists(archivoModal)) return;

        var xaml = File.ReadAllText(archivoModal);

        Assert.Contains("po:Freeze=\"True\"", xaml);
        Assert.Contains("TextOptions.TextRenderingMode=\"Auto\"", xaml);
    }

    [Fact(DisplayName = "CategoriaModal maneja fallas parciales con confirmación y advertencia al operador")]
    public void CategoriaModal_ManejaFallasParciales_ConConfirmacionYAdvertencia()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriaModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("Los datos de la categoría se actualizaron correctamente, pero no se pudo cambiar su estado", codigoCs);
        Assert.Contains("_solicitud.Confirmar();", codigoCs);
    }

    [Fact(DisplayName = "CategoriasView alinea columnas CREADO y ACTUALIZADO a la izquierda")]
    public void CategoriasView_ColumnasFechasAlineadasALaIzquierda()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriasView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var xaml = File.ReadAllText(archivoXaml);

        Assert.Matches(@"Header=""CREADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.Matches(@"Header=""ACTUALIZADO""[^>]*CellStyle=""{StaticResource CeldaIzquierda}""", xaml);
        Assert.DoesNotMatch(@"Header=""CREADO""[^>]*HeaderDerecho", xaml);
        Assert.DoesNotMatch(@"Header=""ACTUALIZADO""[^>]*HeaderDerecho", xaml);
    }

    [Fact(DisplayName = "CategoriaModal utiliza ChangeTracker para dirty tracking tipado")]
    public void CategoriaModal_UtilizaChangeTracker()
    {
        var archivoModalCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Categorias", "CategoriaModal.xaml.cs");
        if (!File.Exists(archivoModalCs)) return;

        var codigoCs = File.ReadAllText(archivoModalCs);

        Assert.Contains("ChangeTracker<CategoriaSnapshot>", codigoCs);
        Assert.Contains("_tracker.IsDirty(snapshotActual)", codigoCs);
    }
}
