using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapaUI.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>Converter={x:Static conv:BoolToVisibilityConverter.Instancia}</c>.
    /// Resuelve contra el tipo CLR sin pasar por ningún diccionario, así que funciona
    /// también en el diseñador de VS, que no ejecuta <c>App.xaml</c> (donde vive la
    /// clave <c>BoolToVisibility</c>). Ver ADR-028.
    /// </summary>
    public static readonly BoolToVisibilityConverter Instancia = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
