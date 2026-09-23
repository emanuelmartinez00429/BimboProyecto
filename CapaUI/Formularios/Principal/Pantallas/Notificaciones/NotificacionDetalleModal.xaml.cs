using System.Windows;
using System.Windows.Controls;
using CapaAplicacion.Notificaciones.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.Notificaciones;

public partial class NotificacionDetalleModal : UserControl
{
    public event Action? Cerrado;

    public NotificacionDto Notificacion { get; private set; } = new();
    public IReadOnlyList<CampoNotificacionDetalle> Campos { get; private set; } = [];
    public string NumeroTexto => Notificacion.IdNotificacion > 0
        ? $"#{Notificacion.IdNotificacion}"
        : "#0";
    public string FechaHoraTexto => Notificacion.FechaCreacion == default
        ? "Sin información"
        : Notificacion.FechaCreacion.ToString("dd/MM/yyyy HH:mm");

    /// <summary>Constructor de diseño. Ver ADR-028.</summary>
    public NotificacionDetalleModal() => InitializeComponent();

    public NotificacionDetalleModal(NotificacionDto notificacion)
    {
        Notificacion = notificacion ?? throw new ArgumentNullException(nameof(notificacion));
        Campos = NotificacionMetadataRenderer.RenderizarCuerpo(notificacion);
        InitializeComponent();
    }

    private void Cerrar_Click(object sender, RoutedEventArgs e) => Cerrado?.Invoke();
}
