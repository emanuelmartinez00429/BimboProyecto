using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace CapaUI.Formularios.Principal.Pantallas.Configuracion;

public partial class ConfiguracionEmpresaModal : UserControl
{
    public ConfiguracionEmpresaViewModel ViewModel { get; }

    public ConfiguracionEmpresaModal(ConfiguracionEmpresaViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) =>
        await ViewModel.InicializarAsync();

    private void SeleccionarLogo_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar logo de empresa",
            Filter = "Imágenes compatibles (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            ViewModel.SeleccionarLogo(dialog.FileName);
    }

    private void SeleccionarIconoSidebar_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar ícono del menú lateral",
            Filter = "Imágenes compatibles (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            ViewModel.SeleccionarIconoSidebar(dialog.FileName);
    }
}
