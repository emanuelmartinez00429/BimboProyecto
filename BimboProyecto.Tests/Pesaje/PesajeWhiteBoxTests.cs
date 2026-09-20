using System;
using System.IO;
using Xunit;

namespace BimboProyecto.Tests.Pesaje;

public class PesajeWhiteBoxTests
{
    [Fact(DisplayName = "PesajeView utiliza LoadingOverlay declarativo con HeaderOffset='40' en Movimiento y Entradas (AP-04)")]
    public void PesajeView_UtilizaLoadingOverlayConHeaderOffset()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Pesaje", "PesajeView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var codigoXaml = File.ReadAllText(archivoXaml);

        // 1. Uso de LoadingOverlay para Movimiento y Entradas
        Assert.Contains("controls:LoadingOverlay x:Name=\"CargandoMovimiento\"", codigoXaml);
        Assert.Contains("controls:LoadingOverlay x:Name=\"CargandoEntradas\"", codigoXaml);

        // 2. Encabezados visibles protegidos mediante HeaderOffset="40"
        Assert.Contains("HeaderOffset=\"40\"", codigoXaml);
    }

    [Fact(DisplayName = "PesajeView no contiene lógica imperativa de Storyboard para spinner de productos")]
    public void PesajeView_NoTieneAnimacionImperativaDeProductos()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Pesaje", "PesajeView.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        // Eliminación de Storyboard y métodos manuales de spinner de productos
        Assert.DoesNotContain("_spinnerProductos", codigoCs);
        Assert.DoesNotContain("IniciarSpinnerProductos", codigoCs);
        Assert.DoesNotContain("DetenerSpinnerProductos", codigoCs);
    }

    [Fact(DisplayName = "LoadingOverlay aplica HeaderOffset al Margin superior para mantener encabezados visibles")]
    public void LoadingOverlay_AplicaHeaderOffsetAlMargenSuperior()
    {
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Core", "Controls", "LoadingOverlay.xaml.cs");
        if (!File.Exists(archivoCs)) return;

        var codigoCs = File.ReadAllText(archivoCs);

        Assert.Contains("Margin = new Thickness(Margin.Left, HeaderOffset, Margin.Right, Margin.Bottom);", codigoCs);
    }

    [Fact(DisplayName = "PesajeView utiliza EmptyStateOverlay para Movimiento y nunca colapsa DgProductos")]
    public void PesajeView_UtilizaEmptyStateOverlayYNoColapsaDgProductos()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Pesaje", "PesajeView.xaml");
        var archivoCs = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Pesaje", "PesajeView.xaml.cs");
        if (!File.Exists(archivoXaml) || !File.Exists(archivoCs)) return;

        var codigoXaml = File.ReadAllText(archivoXaml);
        var codigoCs = File.ReadAllText(archivoCs);

        // MovEmpty es EmptyStateOverlay
        Assert.Contains("controls:EmptyStateOverlay x:Name=\"MovEmpty\"", codigoXaml);

        // DgProductos nunca colapsa su visibilidad (los encabezados permanecen siempre vivos)
        Assert.DoesNotContain("DgProductos.Visibility", codigoCs);
    }

    [Fact(DisplayName = "PesajeView asigna altura compacta de 242px a DgCamiones para 5 ítems exactos")]
    public void PesajeView_DgCamionesAlturaFijaCincoItems()
    {
        var archivoXaml = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "CapaUI", "Formularios", "Principal", "Pantallas", "Pesaje", "PesajeView.xaml");
        if (!File.Exists(archivoXaml)) return;

        var codigoXaml = File.ReadAllText(archivoXaml);

        // DgCamiones con altura exacta para 5 ítems
        Assert.Contains("x:Name=\"DgCamiones\" Grid.Row=\"1\" Height=\"242\"", codigoXaml);
    }
}
