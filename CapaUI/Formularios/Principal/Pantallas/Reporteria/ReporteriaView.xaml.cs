using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Common.Catalogos;
using CapaAplicacion.Productos.Dtos;
using CapaDominio.Reportes;
using CapaUI.Core.Catalogos;
using CapaUI.Core.Controls;
using CapaUI.Formularios.Principal.Pantallas.Bitacora;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace CapaUI.Formularios.Principal.Pantallas.Reporteria;

public partial class ReporteriaView : UserControl
{
    private ReporteriaViewModel? _vm;
    private SelectorCatalogoModal? _selector;
    public ReporteriaView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) return;
        _vm = App.Services.GetRequiredService<ReporteriaViewModel>(); DataContext = _vm;
        _vm.SelectorSolicitado += AbrirSelector; _vm.FormatoSolicitado += AbrirFormato; _vm.ReporteCreado += AbrirReporte;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return; _vm.SelectorSolicitado -= AbrirSelector; _vm.FormatoSolicitado -= AbrirFormato; _vm.ReporteCreado -= AbrirReporte; CerrarModal(); _vm=null;
    }

    private void AbrirSelector(string tipo)
    {
        if (_vm is null) return; var repo=App.Services.GetRequiredService<ICatalogoRepository>();
        var cfg=tipo switch{"producto"=>Catalogos.Productos(repo),"proveedor"=>Catalogos.Proveedores(repo),_=>Catalogos.Categorias(repo)};
        _selector=new SelectorCatalogoModal(cfg);_selector.Cerrado+=CerrarModal;_selector.Seleccionado+=item=>{if(tipo=="producto")_vm.ProductoSeleccionado=item;else if(tipo=="proveedor")_vm.ProveedorSeleccionado=item;else _vm.CategoriaSeleccionada=item;}; // cierra por el evento Cerrado del selector
        MostrarModal(_selector);
    }

    private void AbrirFormato()
    {
        var modal=new FormatoReporteModal();modal.Cerrado+=CerrarModal;modal.FormatoSeleccionado+=async formato=>{CerrarModal();await ElegirRutaYExportarAsync(formato);};MostrarModal(modal);
    }

    private async Task ElegirRutaYExportarAsync(ReportFormat formato)
    {
        if(_vm is null)return;var dialog=new SaveFileDialog{Filter=formato==ReportFormat.Pdf?"Documento PDF (*.pdf)|*.pdf":"Libro de Excel (*.xlsx)|*.xlsx",DefaultExt=formato==ReportFormat.Pdf?".pdf":".xlsx",FileName=$"reporte-{DateTime.Now:yyyyMMdd-HHmmss}"};if(dialog.ShowDialog()==true)await _vm.ExportarAsync(formato,dialog.FileName);
    }

    private static void AbrirReporte(string ruta)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"El reporte se guardó correctamente, pero no pudo abrirse automáticamente.\n\n{ex.Message}",
                "Reporte creado",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void MostrarModal(object contenido){ModalContent.Content=contenido;ModalOverlay.Visibility=Visibility.Visible;}
    private void CerrarModal(){_selector?.Dispose();_selector=null;ModalContent.Content=null;ModalOverlay.Visibility=Visibility.Collapsed;}
}
