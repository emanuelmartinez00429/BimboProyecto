using System.Windows;
using System.Windows.Controls;
using CapaDominio.Reportes;

namespace CapaUI.Formularios.Principal.Pantallas.Bitacora;

public partial class FormatoReporteModal : UserControl
{
    public event Action<ReportFormat>? FormatoSeleccionado;
    public event Action? Cerrado;

    public FormatoReporteModal() => InitializeComponent();

    private void BtnPdf_Click(object sender, RoutedEventArgs e) =>
        FormatoSeleccionado?.Invoke(ReportFormat.Pdf);

    private void BtnExcel_Click(object sender, RoutedEventArgs e) =>
        FormatoSeleccionado?.Invoke(ReportFormat.Excel);

    private void BtnCancelar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
}
