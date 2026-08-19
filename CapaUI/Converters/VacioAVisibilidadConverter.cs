using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapaUI.Converters;

/// <summary>
/// Para el placeholder de un campo (texto de ejemplo tipo "0.00" superpuesto
/// al TextBox, oculto en cuanto el usuario escribe algo). Visible cuando el
/// string está vacío/blanco, Collapsed cuando tiene contenido.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public class VacioAVisibilidadConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
