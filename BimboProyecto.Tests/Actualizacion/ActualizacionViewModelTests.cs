using System;
using System.Threading;
using System.Threading.Tasks;
using CapaUI.Formularios.Principal;
using CapaUI.Services.Actualizacion;
using Xunit;

namespace BimboProyecto.Tests.Actualizacion;

// =============================================================================
//  Maquina de estados del banner de actualizacion — ActualizacionViewModel
//
//  El ViewModel no conoce Velopack ni WPF: se ejercita con un fake del servicio,
//  asi que estos tests cubren la logica que decide si el banner aparece, que
//  texto muestra y cuando se dispara el reinicio.
// =============================================================================

internal sealed class ServicioActualizacionFalso : IActualizacionService
{
    public bool EstaInstalada { get; set; } = true;
    public string VersionActual { get; set; } = "1.0.0";
    public ActualizacionDisponible? Disponible { get; set; }
    public bool DescargaOk { get; set; } = true;

    public int VecesBuscar { get; private set; }
    public int VecesDescargar { get; private set; }
    public int VecesAplicarAlCerrar { get; private set; }
    public int VecesReiniciar { get; private set; }

    public Task<ActualizacionDisponible?> BuscarAsync(CancellationToken ct = default)
    {
        VecesBuscar++;
        return Task.FromResult(Disponible);
    }

    public Task<bool> DescargarAsync(IProgress<int>? progreso = null, CancellationToken ct = default)
    {
        VecesDescargar++;
        return Task.FromResult(DescargaOk);
    }

    public void AplicarAlCerrar() => VecesAplicarAlCerrar++;

    public void ReiniciarYAplicar() => VecesReiniciar++;
}

public sealed class ActualizacionViewModelTests
{
    [Fact]
    public async Task IniciarAsync_SinActualizacion_NoMuestraBanner()
    {
        var servicio = new ServicioActualizacionFalso { Disponible = null };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.False(vm.BannerVisible);
        Assert.False(vm.ListaParaAplicar);
        Assert.Equal(0, servicio.VecesDescargar);
    }

    [Fact]
    public async Task IniciarAsync_ConActualizacion_DescargaYQuedaLista()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
        };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.True(vm.BannerVisible);
        Assert.True(vm.ListaParaAplicar);
        Assert.False(vm.Descargando);
        Assert.Equal("1.0.1", vm.VersionDisponible);
        Assert.Equal(1, servicio.VecesDescargar);
    }

    [Fact]
    public async Task IniciarAsync_PaqueteYaDescargado_NoVuelveADescargar()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: true),
        };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.True(vm.BannerVisible);
        Assert.True(vm.ListaParaAplicar);
        Assert.Equal(0, servicio.VecesDescargar);
    }

    [Fact]
    public async Task IniciarAsync_NoInstalada_NoVerificaNiMuestraBanner()
    {
        var servicio = new ServicioActualizacionFalso
        {
            EstaInstalada = false,
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
        };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.Equal(0, servicio.VecesBuscar);
        Assert.False(vm.BannerVisible);
    }

    [Fact]
    public async Task IniciarAsync_DescargaFallida_OcultaBanner()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
            DescargaOk = false,
        };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.False(vm.BannerVisible);
        Assert.False(vm.ListaParaAplicar);
    }

    [Fact]
    public async Task IniciarAsync_DosVeces_SoloVerificaUnaVez()
    {
        var servicio = new ServicioActualizacionFalso { Disponible = null };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();
        await vm.IniciarAsync();

        Assert.Equal(1, servicio.VecesBuscar);
    }

    [Fact]
    public void Ocultar_OcultaElBanner()
    {
        var vm = new ActualizacionViewModel(new ServicioActualizacionFalso())
        {
            BannerVisible = true,
        };

        vm.OcultarCommand.Execute(null);

        Assert.False(vm.BannerVisible);
    }

    [Fact]
    public async Task ReiniciarAhora_SoloEmiteElEvento_NoAplicaPorSuCuenta()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
        };
        var vm = new ActualizacionViewModel(servicio);
        await vm.IniciarAsync();

        int eventos = 0;
        vm.ReinicioSolicitado += (_, _) => eventos++;

        vm.ReiniciarAhoraCommand.Execute(null);

        Assert.Equal(1, eventos);
        Assert.Equal(0, servicio.VecesReiniciar);
    }

    [Fact]
    public async Task AplicarReinicio_LlamaAlServicioUnaSolaVez()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
        };
        var vm = new ActualizacionViewModel(servicio);
        await vm.IniciarAsync();

        vm.AplicarReinicio();
        vm.AplicarReinicio();

        Assert.Equal(1, servicio.VecesReiniciar);
        Assert.True(vm.Aplicando);
        Assert.False(vm.ReiniciarAhoraCommand.CanExecute(null));
    }

    [Fact]
    public void AplicarReinicio_SinPaqueteListo_NoHaceNada()
    {
        var servicio = new ServicioActualizacionFalso();
        var vm = new ActualizacionViewModel(servicio);

        vm.AplicarReinicio();

        Assert.Equal(0, servicio.VecesReiniciar);
    }

    [Fact]
    public async Task Mensaje_CuandoEstaLista_IndicaQueSeAplicaraAlCerrar()
    {
        var servicio = new ServicioActualizacionFalso
        {
            Disponible = new ActualizacionDisponible("1.0.1", ListaParaAplicar: false),
        };
        var vm = new ActualizacionViewModel(servicio);

        await vm.IniciarAsync();

        Assert.Contains("1.0.1", vm.Mensaje);
        Assert.Contains("se aplicará al cerrar", vm.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VersionTexto_IncluyeLaVersionDelServicio()
    {
        var vm = new ActualizacionViewModel(new ServicioActualizacionFalso { VersionActual = "2.3.4" });

        Assert.Equal("v2.3.4", vm.VersionTexto);
    }
}
