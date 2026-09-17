using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CapaUI.Formularios.Principal.Pantallas.Configuracion;

public partial class ConfiguracionEmpresaView : UserControl
{
    private ConfiguracionEmpresaViewModel? Vm => DataContext as ConfiguracionEmpresaViewModel;

    public ConfiguracionEmpresaView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this)) return;

        if (Vm != null)
        {
            await Vm.InicializarAsync();
        }
    }

    private void SeleccionarLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar ícono del menú lateral",
            Filter = "Imágenes compatibles (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            Vm?.SeleccionarLogo(dialog.FileName);
    }

    private void SeleccionarIconoSidebar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar logo de empresa",
            Filter = "Imágenes compatibles (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            Vm?.SeleccionarIconoSidebar(dialog.FileName);
    }
}
