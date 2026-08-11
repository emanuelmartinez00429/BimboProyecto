using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CapaUI.Converters;

/// <summary>
/// Convierte un color hexadecimal (<c>#10B981</c>) en un pincel congelado.
/// Cachea por color: dos elementos con el mismo hex comparten el mismo
/// <see cref="SolidColorBrush"/> en vez de alojar uno cada uno.
/// </summary>
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class ColorHexABrushConverter : IValueConverter
{
    private static readonly Dictionary<string, Brush> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex || string.IsNullOrWhiteSpace(hex))
            return DependencyProperty.UnsetValue;

        lock (Cache)
        {
            if (Cache.TryGetValue(hex, out var cacheado))
                return cacheado;

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            Cache[hex] = brush;
            return brush;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
