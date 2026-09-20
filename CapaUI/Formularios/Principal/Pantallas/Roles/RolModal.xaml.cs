using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Formularios.Principal.Pantallas.Roles;

/// <summary>
/// Alta y edición del nombre de un rol.
/// <para/>
/// Es un <see cref="UserControl"/> que vive sobre el overlay de <c>RolesView</c>, no una
/// ventana: así hereda el <c>LayoutTransform</c> del escalado propio de la app y sigue el
/// mismo molde que los otros modales. Avisa qué pasó por <see cref="Cerrado"/> y
/// <see cref="Guardado"/> en vez de devolver un <c>DialogResult</c>.
/// </summary>
public partial class RolModal : UserControl
{
    private readonly RolesViewModel _vm;
    private readonly RolItemVm? _rol;

    /// <summary>Se canceló: la vista tiene que sacar el modal del overlay.</summary>
    public event Action? Cerrado;

    /// <summary>Se guardó bien. El ViewModel ya actualizó su colección; acá solo falta cerrar.</summary>
    public event Action? Guardado;

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

        if (guardado) Guardado?.Invoke();
        else          MostrarError(_vm.Mensaje);
    }

    private void MostrarError(string mensaje)
    {
        TxtError.Text = mensaje;
        TxtError.Visibility = Visibility.Visible;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
}
