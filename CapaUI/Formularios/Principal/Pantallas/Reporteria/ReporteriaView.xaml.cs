using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private Storyboard? _spinnerStory;

    public ReporteriaView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null) return;
        _vm = App.CrearVm<ReporteriaViewModel>();
        DataContext = _vm;
        _vm.SelectorSolicitado += AbrirSelector;
        _vm.FormatoSolicitado += AbrirFormato;
        _vm.ReporteCreado += AbrirReporte;
        _vm.PropertyChanged += OnVmPropertyChanged;
        RefrescarPaginacion();
        ActualizarVisibilidadTabla();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        _vm.SelectorSolicitado -= AbrirSelector;
        _vm.FormatoSolicitado -= AbrirFormato;
        _vm.ReporteCreado -= AbrirReporte;
        _vm.PropertyChanged -= OnVmPropertyChanged;
        CerrarModal();
        DetenerSpinner();
        _vm = null;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ReporteriaViewModel.VistaPrevia):
            case nameof(ReporteriaViewModel.SinResultados):
            case nameof(ReporteriaViewModel.HayResultados):
                RefrescarPaginacion();
                ActualizarVisibilidadTabla();
                break;
            case nameof(ReporteriaViewModel.Pagina):
            case nameof(ReporteriaViewModel.TotalPages):
                RefrescarPaginacion();
                break;
            case nameof(ReporteriaViewModel.IsBusy):
                ActualizarCarga();
                break;
        }
    }

    private void ActualizarCarga()
    {
        if (_vm is null) return;
        if (_vm.IsBusy)
        {
            DgPreview.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            IniciarSpinner();
        }
        else
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            DetenerSpinner();
            ActualizarVisibilidadTabla();
        }
    }

    private void ActualizarVisibilidadTabla()
    {
        if (_vm is null || _vm.IsBusy) return;
        if (_vm.SinResultados)
        {
            DgPreview.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyState.Visibility = Visibility.Collapsed;
            DgPreview.Visibility = Visibility.Visible;
        }
    }

    private void DgPreview_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is DataGridTextColumn textCol)
        {
            string header = e.PropertyName;
            var elementStyle = new Style(typeof(TextBlock));
            elementStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")));
            elementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 14.5));
            elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x1A, 0x1F, 0x2E))));
            elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            elementStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10, 0, 10, 0)));

            if (header.StartsWith("Peso", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("(kg)", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("(USD)", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("(%)", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("Bultos", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("Entradas", StringComparison.OrdinalIgnoreCase) ||
                header.Contains("Diferencia", StringComparison.OrdinalIgnoreCase))
            {
                elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left));
            }
            else if (header.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("#", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Estado", StringComparison.OrdinalIgnoreCase) ||
                     header.StartsWith("Fecha", StringComparison.OrdinalIgnoreCase) ||
                     header.Equals("Placa", StringComparison.OrdinalIgnoreCase))
            {
                elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            }
            else
            {
                elementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left));
                elementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            }

            textCol.ElementStyle = elementStyle;
        }
    }

    private void RefrescarPaginacion()
    {
        if (_vm is null) return;
        PaginacionPanel.Items.Clear();
        int total = _vm.TotalPages;
        int current = _vm.Pagina;
        if (_vm.VistaPrevia is null || total <= 1) return;

        foreach (int p in Paginacion.Calcular(current, total))
        {
            if (p == Paginacion.Elipsis)
            {
                PaginacionPanel.Items.Add(new TextBlock
                {
                    Text = "\u2026",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF))
                });
            }
            else
            {
                var btn = new Button
                {
                    Content = p.ToString(),
                    Margin = new Thickness(2, 0, 2, 0),
                    Style = (Style)(p == current ? FindResource("ActivePageBtn") : FindResource("PageBtn")),
                    Tag = p
                };
                btn.Click += (s, ev) =>
                {
                    if (_vm.IsBusy) return;
                    if (s is Button b && b.Tag is int pg)
                    {
                        _vm.IrAPaginaCommand.Execute(pg);
                    }
                };
                PaginacionPanel.Items.Add(btn);
            }
        }
    }

    private void IniciarSpinner()
    {
        if (_spinnerStory is not null) return;
        _spinnerStory = new Storyboard();
        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.8))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        Storyboard.SetTarget(anim, SpinnerPath);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
        _spinnerStory.Children.Add(anim);
        _spinnerStory.Begin();
    }

    private void DetenerSpinner()
    {
        if (_spinnerStory is null) return;
        _spinnerStory.Stop();
        _spinnerStory.Remove();
        _spinnerStory.Children.Clear();
        _spinnerStory = null;
    }

    private void AbrirSelector(string tipo)
    {
        if (_vm is null) return;
        if (tipo == "producto" && _vm.ProveedorSeleccionado?.Id is null) return;

        CerrarModal();
        var repo = App.Services.GetRequiredService<ICatalogoRepository>();
        var cfgBase = tipo switch
        {
            "producto"  => Catalogos.Productos(repo, _vm.ProveedorSeleccionado!.Id),
            "proveedor" => Catalogos.Proveedores(repo),
            _            => Catalogos.Categorias(repo),
        };
        var cfg = cfgBase with { PageSize = 50, ForzarPaginacion = true };

        _selector=new SelectorCatalogoModal(cfg);_selector.Cerrado+=CerrarModal;_selector.Seleccionado+=item=>{if(tipo=="producto")_vm.ProductoSeleccionado=item;else if(tipo=="proveedor")_vm.ProveedorSeleccionado=item;else _vm.CategoriaSeleccionada=item;}; // cierra por el evento Cerrado del selector
        MostrarSelector(_selector);
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

    private void MostrarSelector(SelectorCatalogoModal selector)
    {
        ModalContent.Visibility   = Visibility.Collapsed;
        SelectorContent.Content   = selector;
        SelectorFrame.Visibility = Visibility.Visible;
        ModalOverlay.Visibility   = Visibility.Visible;
    }

    private void MostrarModal(object contenido)
    {
        SelectorFrame.Visibility = Visibility.Collapsed;
        SelectorContent.Content  = null;
        ModalContent.Content     = contenido;
        ModalContent.Visibility  = Visibility.Visible;
        ModalOverlay.Visibility  = Visibility.Visible;
    }

    private void CerrarModal()
    {
        _selector?.Dispose();
        _selector                 = null;
        SelectorContent.Content   = null;
        SelectorFrame.Visibility = Visibility.Collapsed;
        ModalContent.Content      = null;
        ModalContent.Visibility   = Visibility.Visible;
        ModalOverlay.Visibility   = Visibility.Collapsed;
    }
}
