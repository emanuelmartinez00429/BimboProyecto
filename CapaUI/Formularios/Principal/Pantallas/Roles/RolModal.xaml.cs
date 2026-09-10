using System.Windows;
using System.Windows.Input;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

public partial class RolModal : Window
{
    private readonly RolesViewModel _vm;
    private readonly RolItemVm? _rol;

    /// <summary>Constructor de diseño (el diseñador de VS instancia por acá). Ver ADR-028.</summary>
    public RolModal()
    {
        _vm = null!;
        InitializeComponent();
    }

    public RolModal(RolesViewModel vm, RolItemVm? rol)
    {
        InitializeComponent();
        _vm = vm;
        _rol = rol;

        if (rol is not null)
        {
            TxtContexto.Text = "EDITAR ROL";
            TxtTitulo.Text = "Modificar rol";
            TxtNombre.Text = rol.Nombre;
            TxtNombre.SelectAll();
        }

        Loaded += (_, _) => TxtNombre.Focus();
    }

    private async void Guardar_Click(object sender, RoutedEventArgs e)
    {
        string nombre = TxtNombre.Text.Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            MostrarError("El nombre del rol es obligatorio.");
            return;
        }

        BtnGuardar.IsEnabled = false;
        bool guardado = await _vm.GuardarRolAsync(_rol, nombre);
        BtnGuardar.IsEnabled = true;
        if (guardado)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MostrarError(_vm.Mensaje);
        }
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    private void Encabezado_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
