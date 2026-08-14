using System;
using System.Globalization;
using System.Windows.Data;

namespace CapaUI.Converters
{
    /// Resta un margen fijo (en pixeles) a un valor double. Se usa para dejar
    /// "aire" entre un modal y los bordes del ModalOverlay que lo contiene,
    /// ya que el MaxWidth/MaxHeight del modal esta enlazado al tamano real
    /// del overlay (que ocupa toda la vista). ConverterParameter = cantidad
    /// total a restar (ej. "48" = 24px de aire por lado). Nunca baja de 0.
    public sealed class RestarMargenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                double margen = 48;
                if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                    margen = p;
                return Math.Max(0, d - margen);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
