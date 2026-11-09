using CapaAplicacion.Common;
using CapaAplicacion.Empresa.Dtos;
using CapaAplicacion.Empresa.Interfaces;
using CapaAplicacion.Productos.Dtos;
using CapaAplicacion.Reportes.Dtos;
using CapaAplicacion.Reportes.Interfaces;
using CapaAplicacion.Usuarios.Interfaces;
using CapaDominio.Entities;
using CapaDominio.Reportes;
using CapaUI.Core.Empresa;
using CapaUI.Core.Permisos;
using CapaUI.Formularios.Principal.Pantallas.Reporteria;
using Xunit;

namespace BimboProyecto.Tests.Reportes;

/// <summary>
/// Cubre el reporte de catálogo completo: ya no son 10 filas fijas, y la categoría
/// seleccionada viaja al filtro del repositorio (sin selección = todos los productos).
/// </summary>
public sealed class ReporteriaViewModelTests
{
    private const int IdUsuario = 7;

    [Fact]
    public async Task Productos_SinCategoria_ConsultaTodoElCatalogo()
    {
        var (vm, repo) = Crear();
        vm.SeleccionarReporteCommand.Execute(ReporteOperativoTipo.Productos);

        await vm.ConsultarCommand.ExecuteAsync(null);

        Assert.NotNull(repo.UltimoFiltroProductos);
        Assert.Null(repo.UltimoFiltroProductos!.IdCategoria);
        Assert.Equal(IdUsuario, repo.UltimoFiltroProductos.IdUsuario);
        Assert.Equal(2, vm.VistaPrevia!.Count);
        Assert.Equal("Catálogo de productos", vm.TituloActual);
        Assert.Contains(vm.MetadatosVistaPrevia, m => m is { Label: "Categoría", Value: "Todas las categorías" });
    }

    [Fact]
    public async Task Productos_ConCategoria_EnviaLaSeleccionAlFiltro()
    {
        var (vm, repo) = Crear();
        vm.SeleccionarReporteCommand.Execute(ReporteOperativoTipo.Productos);
        vm.CategoriaSeleccionada = new FiltroItem { Id = 12, Nombre = "Materias Primas" };

        await vm.ConsultarCommand.ExecuteAsync(null);

        Assert.Equal(12, repo.UltimoFiltroProductos!.IdCategoria);
        Assert.Contains(vm.MetadatosVistaPrevia, m => m is { Label: "Categoría", Value: "Materias Primas" });
    }

    [Fact]
    public async Task Productos_CambiarCategoria_InvalidaLosResultados()
    {
        var (vm, _) = Crear();
        vm.SeleccionarReporteCommand.Execute(ReporteOperativoTipo.Productos);
        await vm.ConsultarCommand.ExecuteAsync(null);

        vm.CategoriaSeleccionada = new FiltroItem { Id = 12, Nombre = "Materias Primas" };

        Assert.Null(vm.VistaPrevia);
        Assert.False(vm.HayResultados);
    }

    [Fact]
    public void ProductosY_Mermas_CompartenElFiltroDeCategoria()
    {
        var (vm, _) = Crear();

        vm.SeleccionarReporteCommand.Execute(ReporteOperativoTipo.Productos);
        Assert.True(vm.PermiteCategoria);
        Assert.True(vm.EsProductos);
        Assert.False(vm.RequiereFechas);

        vm.SeleccionarReporteCommand.Execute(ReporteOperativoTipo.ProductosConMerma);
        Assert.True(vm.PermiteCategoria);
        Assert.True(vm.RequiereFechas);
    }

    private static (ReporteriaViewModel Vm, FakeReporteConsultaRepository Repo) Crear()
    {
        var sesion = new FakeUsuarioSesionService
        {
            SesionActual = new UsuarioSesion([new ModuloPermisos { NombreModulo = "Reportes", Acciones = ["REPORTES_CONSULTAR", "REPORTES_GENERAR"] }])
            {
                IdUsuario = IdUsuario, Email = "admin@empresa.com", NombreRol = "Administrador",
            },
        };
        SesionPermisos.Configurar(sesion);

        var repo = new FakeReporteConsultaRepository();
        var empresa = new FakeEmpresaRepository();
        var vm = new ReporteriaViewModel(repo, new FakeReporteRepository(), new FakeReportGeneratorService(),
            sesion, empresa, new LogoEmpresaCache(empresa));
        return (vm, repo);
    }

    private sealed class FakeUsuarioSesionService : IUsuarioSesionService
    {
        public UsuarioSesion? SesionActual { get; set; }
        public bool Autenticado => SesionActual is not null;
        public Task<Result<UsuarioSesion>> IniciarSesionAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(Result<UsuarioSesion>.Ok(SesionActual!));
        public void CerrarSesion() => SesionActual = null;
        public bool TienePermiso(string nombreAccion) => true;
    }

    private sealed class FakeReporteConsultaRepository : IReporteConsultaRepository
    {
        public ProductosReporteFiltro? UltimoFiltroProductos { get; private set; }

        public IReadOnlyList<ProductoReporteFila> Productos { get; set; } =
        [
            new() { IdProducto = 1, Codigo = "P-001", Nombre = "Azúcar", Categoria = "Materias Primas", Proveedor = "Ingenio", Estado = "Activo" },
            new() { IdProducto = 2, Codigo = "P-002", Nombre = "Botella 500 ml", Categoria = "Botellas", Proveedor = "Envases HN", Estado = "Activo" },
        ];

        public Task<Result<IReadOnlyList<EntradaMateriaPrimaFila>>> ConsultarEntradaMateriaPrimaAsync(
            EntradaMateriaPrimaFiltro filtro, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<EntradaMateriaPrimaFila>>.Ok(Array.Empty<EntradaMateriaPrimaFila>()));

        public Task<Result<IReadOnlyList<ProveedorReporteFila>>> ConsultarProveedorAsync(
            ProveedorReporteFiltro filtro, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<ProveedorReporteFila>>.Ok(Array.Empty<ProveedorReporteFila>()));

        public Task<Result<IReadOnlyList<MermaReporteFila>>> ConsultarMermasAsync(
            MermasReporteFiltro filtro, CancellationToken ct = default) =>
            Task.FromResult(Result<IReadOnlyList<MermaReporteFila>>.Ok(Array.Empty<MermaReporteFila>()));

        public Task<Result<IReadOnlyList<ProductoReporteFila>>> ConsultarProductosAsync(
            ProductosReporteFiltro filtro, CancellationToken ct = default)
        {
            UltimoFiltroProductos = filtro;
            return Task.FromResult(Result<IReadOnlyList<ProductoReporteFila>>.Ok(Productos));
        }
    }

    private sealed class FakeReporteRepository : IReporteRepository
    {
        public Task<Result<int>> RegistrarAsync(ReporteRegistroDto reporte, CancellationToken ct = default) =>
            Task.FromResult(Result<int>.Ok(1));
    }

    private sealed class FakeReportGeneratorService : IReportGeneratorService
    {
        public Task<Result<byte[]>> GenerateAsync(TabularReportDto report, ReportFormat format, CancellationToken ct = default) =>
            Task.FromResult(Result<byte[]>.Ok([]));
    }

    private sealed class FakeEmpresaRepository : IEmpresaRepository
    {
        public Task<Result<EmpresaDto?>> ObtenerAsync(CancellationToken ct = default) =>
            Task.FromResult(Result<EmpresaDto?>.Ok(null));

        public Task<Result<EmpresaGuardadaDto>> GuardarAsync(ActualizarEmpresaDto empresa, string? rutaLogoLocal,
            string? rutaIconoSidebarLocal, CancellationToken ct = default) =>
            throw new NotSupportedException("El reporte de productos no guarda configuración de empresa.");

        public Task<Result> DescargarLogoAsync(string rutaStorage, string rutaLocalDestino, CancellationToken ct = default) =>
            Task.FromResult(Result.Ok());
    }
}
