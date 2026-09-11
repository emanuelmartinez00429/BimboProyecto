using System;
using System.Globalization;
using System.Windows.Data;

namespace CapaUI.Converters;

/// <summary>
/// Convierte un valor de enumeración en un booleano para enlazar grupos de RadioButtons en XAML
/// de forma declarativa y bidireccional (Mode=TwoWay).
/// Al desmarcarse un RadioButton (value == false), retorna Binding.DoNothing para evitar
/// que la deselección sobrescriba el nuevo valor de la enumeración seleccionado por el otro botón.
/// </summary>
[ValueConversion(typeof(Enum), typeof(bool))]
public class EnumToBooleanConverter : IValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>{x:Static conv:EnumToBooleanConverter.Instancia}</c>.
    /// Resuelve contra el tipo CLR sin diccionario, garantizando compatibilidad con el
    /// diseñador de Visual Studio conforme a ADR-028.
    /// </summary>
    public static readonly EnumToBooleanConverter Instancia = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        string checkValue = value.ToString()!;
        string targetValue = parameter.ToString()!;
        return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is not null)
        {
            try
            {
                Type enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                return Enum.Parse(enumType, parameter.ToString()!, true);
            }
            catch (ArgumentException)
            {
                return Binding.DoNothing;
            }
        }

        return Binding.DoNothing;
    }
}
