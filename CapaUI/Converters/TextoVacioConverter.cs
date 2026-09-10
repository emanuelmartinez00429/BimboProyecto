using System;
using System.Globalization;
using System.Windows.Data;

namespace CapaUI.Converters;

/// <summary>
/// Convierte valores nulos o cadenas vacías/espacios en blanco en un guion largo tipográfico ("—")
/// para estandarizar la representación visual de campos sin registrar en tablas y formularios.
/// Permite sobreescribir el texto de reemplazo mediante ConverterParameter.
/// </summary>
[ValueConversion(typeof(object), typeof(string))]
public class TextoVacioConverter : IValueConverter
{
    /// <summary>
    /// Instancia compartida para <c>{x:Static conv:TextoVacioConverter.Instancia}</c>.
    /// Resuelve contra el tipo CLR sin diccionario, así que sirve también en el diseñador
    /// de VS, que no ejecuta <c>App.xaml</c> (donde vive la clave). Ver ADR-028.
    /// </summary>
    public static readonly TextoVacioConverter Instancia = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return parameter?.ToString() ?? "—";

        if (value is string s)
            return string.IsNullOrWhiteSpace(s) ? (parameter?.ToString() ?? "—") : s;

        return value.ToString() ?? (parameter?.ToString() ?? "—");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
