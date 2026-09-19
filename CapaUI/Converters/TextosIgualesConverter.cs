using System.Globalization;
using System.Windows.Data;

namespace CapaUI.Converters;

/// <summary>
/// <c>true</c> si todos los valores del <c>MultiBinding</c> son el mismo texto
/// (sin distinguir mayúsculas). Pensado para marcar la opción elegida de un grupo de
/// botones sin <c>RadioButton</c>, p. ej. el color activo contra el
/// <c>CommandParameter</c> de cada muestra en <c>ConfiguracionEmpresaView</c>.
/// </summary>
public sealed class TextosIgualesConverter : IMultiValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>Converter={x:Static conv:TextosIgualesConverter.Instancia}</c>
    /// (ver ADR-028).
    /// </summary>
    public static readonly TextosIgualesConverter Instancia = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string primero) return false;

        for (var i = 1; i < values.Length; i++)
        {
            if (values[i] is not string otro ||
                !string.Equals(primero.Trim(), otro.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
