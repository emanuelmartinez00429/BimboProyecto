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

    public string RegistroId { get; private set; } = string.Empty;
    public string FechaHoraTexto { get; private set; } = string.Empty;
    public string ModuloBadge { get; private set; } = string.Empty;

    public BitacoraDetalleModal()
    {
        InitializeComponent();
        DataContext = Array.Empty<BitacoraDetalleCampo>();
    }

    public BitacoraDetalleModal(BitacoraDto registro) : this()
    {
        _registro = registro ?? throw new ArgumentNullException(nameof(registro));
        RegistroId = $"#{registro.IdBitacora}";
        FechaHoraTexto = registro.FechaHora?.ToString("dd/MM/yyyy HH:mm") ?? BitacoraDetalle.SinInformacion;
        ModuloBadge = string.IsNullOrWhiteSpace(registro.NombreModulo) ? "SISTEMA" : registro.NombreModulo.ToUpperInvariant();

        TxtRegistroId.Text = RegistroId;
        TxtFechaHora.Text = FechaHoraTexto;
        TxtModuloBadge.Text = ModuloBadge;

        DataContext = BitacoraDetalle.CrearCamposSinHero(registro);
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
