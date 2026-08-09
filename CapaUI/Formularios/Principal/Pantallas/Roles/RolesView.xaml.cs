using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public partial class RolesView : UserControl
{
    private RolesViewModel? _vm;

    public RolesView()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm is not null)
            return;

        _vm = App.Services.GetRequiredService<RolesViewModel>();
        DataContext = _vm;
        await _vm.CargarAsync();
    }
}
