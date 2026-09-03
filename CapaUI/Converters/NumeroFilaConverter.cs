using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace CapaUI.Converters;

/// <summary>
/// Número de fila para las tablas de lista. Es una posición visual, no un dato de
/// la base: <c>values = [ item, DataGridRow, Página actual ]</c>. El primer binding
/// (el item) fuerza el recálculo cuando el contenedor se recicla al hacer scroll
/// con virtualización. La numeración es global: sigue entre páginas
/// ((página - 1) * tamañoPágina + posición + 1).
/// </summary>
public sealed class NumeroFilaConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [_, DataGridRow row, ..]) return string.Empty;

        int indice = row.GetIndex();
        if (indice < 0) return string.Empty;

        int pagina = values.Length > 2 && values[2] is int p && p > 0 ? p : 1;
        int tamano = int.TryParse(parameter as string, out var t) && t > 0 ? t : 50;

        return ((pagina - 1) * tamano + indice + 1).ToString(culture);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
