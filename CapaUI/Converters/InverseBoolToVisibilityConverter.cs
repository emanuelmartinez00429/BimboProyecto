using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapaUI.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>Converter={x:Static conv:InverseBoolToVisibilityConverter.Instancia}</c>.
    /// Resuelve contra el tipo CLR sin diccionario de por medio, así que funciona también
    /// en el diseñador de VS, que no ejecuta <c>App.xaml</c>. Ver ADR-028.
    /// </summary>
    public static readonly InverseBoolToVisibilityConverter Instancia = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}
