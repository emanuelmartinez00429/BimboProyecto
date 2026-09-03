using CapaAplicacion.Notificaciones.Dtos;
using System.Windows;

namespace CapaUI.Formularios.Principal.Pantallas.Notificaciones;

public partial class NotificacionDetalleWindow : Window
{
    public NotificacionDto Notificacion { get; }
    public IReadOnlyList<CampoNotificacionDetalle> Campos { get; }

    public NotificacionDetalleWindow(NotificacionDto notificacion)
    {
        Notificacion = notificacion;
        Campos = NotificacionMetadataRenderer.Renderizar(notificacion);
        InitializeComponent();
        DataContext = this;
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Close();
}
