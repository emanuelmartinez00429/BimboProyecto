using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CapaAplicacion.Notificaciones.Dtos;

namespace CapaUI.Formularios.Principal.Pantallas.Notificaciones;

public sealed class SoportaNavegacionNotificacionConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is NotificacionDto { IdRegistroOrigen: > 0, TablaOrigen: "usuarios" or "roles" }
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
