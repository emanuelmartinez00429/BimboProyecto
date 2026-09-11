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
public sealed class EnumToBooleanConverter : IValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>{x:Static conv:EnumToBooleanConverter.Instancia}</c>.
    /// Resuelve contra el tipo CLR sin diccionario, garantizando compatibilidad con el
    /// diseñador de Visual Studio conforme a ADR-028.
    /// </summary>
    public static readonly EnumToBooleanConverter Instancia = new();

    /// <summary>
    /// Alias canónico según la investigación técnica.
    /// </summary>
    public static EnumToBooleanConverter Instance => Instancia;

    /// <summary>
    /// Constructor explícito sin parámetros requerido por diseñadores visuales y XAML.
    /// </summary>
    public EnumToBooleanConverter()
    {
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null)
            return false;

        // Fast-path: comparación directa de enums sin asignar cadenas en el heap (Gen0)
        Type valueType = value.GetType();
        Type paramType = parameter.GetType();

        if (valueType.IsEnum && paramType.IsEnum)
        {
            if (valueType.IsDefined(typeof(FlagsAttribute), inherit: false))
            {
                ulong numericValue = System.Convert.ToUInt64(value, culture);
                ulong numericParameter = System.Convert.ToUInt64(parameter, culture);

                if (numericParameter == 0)
                {
                    return numericValue == 0;
                }

                return (numericValue & numericParameter) == numericParameter;
            }

            return value.Equals(parameter);
        }

        // Fallback para bindings XAML con ConverterParameter como string
        string checkValue = value.ToString()!;
        string targetValue = parameter.ToString()!;
        return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked && parameter is not null)
        {
            Type enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Fast-path: si el parámetro ya viene tipado como el Enum (x:Static), evitar Enum.Parse
            if (parameter.GetType() == enumType)
                return parameter;

            try
            {
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
