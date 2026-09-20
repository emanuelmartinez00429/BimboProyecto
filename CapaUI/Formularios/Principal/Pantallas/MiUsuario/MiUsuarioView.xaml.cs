using System.Windows.Controls;

namespace CapaUI.Formularios.Principal.Pantallas.MiUsuario;

/// <summary>
/// Vista de «Mi Usuario». Sin lógica propia: todo el comportamiento vive en
/// <see cref="MiUsuarioViewModel"/>, que llega por <c>DataContext</c> desde el
/// <c>DataTemplate</c> de <c>MainWindow</c>.
/// </summary>
public partial class MiUsuarioView : UserControl
{
    public MiUsuarioView() => InitializeComponent();
}
