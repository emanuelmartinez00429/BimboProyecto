using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Bitacora;
using CapaAplicacion.Bitacora.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.Bitacora;

public partial class BitacoraDetalleModal : UserControl
{
    private BitacoraDto? _registro;

    public event Action? Cerrado;
    public event Action<BitacoraDto>? ImprimirSolicitado;

    public BitacoraDetalleModal()
    {
        InitializeComponent();
        DataContext = Array.Empty<BitacoraDetalleCampo>();
    }

    public BitacoraDetalleModal(BitacoraDto registro) : this()
    {
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        DataContext = BitacoraDetalle.CrearCampos(registro);
    }

    public void MostrarError(string? mensaje)
    {
        ErrorText.Text = mensaje ?? string.Empty;
        ErrorPanel.Visibility = string.IsNullOrWhiteSpace(mensaje)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    public void EstablecerGenerando(bool generando)
    {
        BtnCerrar.IsEnabled = !generando;
        BtnRegresar.IsEnabled = !generando;
        BtnImprimir.IsEnabled = !generando;
    }

    private void BtnRegresar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();

    private void BtnImprimir_Click(object sender, RoutedEventArgs e)
    {
        if (_registro is not null)
            ImprimirSolicitado?.Invoke(_registro);
    }
}
