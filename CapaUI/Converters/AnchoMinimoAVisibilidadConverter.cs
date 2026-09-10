using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapaUI.Converters
{
    /// Muestra un elemento solo a partir de cierto ancho del contenedor.
    /// Se usa para las etiquetas de botones que, en ventana angosta, le comen el
    /// espacio a lo que sí tiene que quedar usable (el buscador): por debajo del
    /// umbral el botón se queda con el icono solo.
    /// value = ancho real del contenedor; ConverterParameter = umbral en píxeles.
    public sealed class AnchoMinimoAVisibilidadConverter : IValueConverter
    {
        /// <summary>
        /// Instancia compartida para <c>{x:Static conv:AnchoMinimoAVisibilidadConverter.Instancia}</c>.
        /// Resuelve contra el tipo CLR sin diccionario, así que sirve también en el diseñador
        /// de VS, que no ejecuta <c>App.xaml</c> (donde vive la clave). Ver ADR-028.
        /// </summary>
        public static readonly AnchoMinimoAVisibilidadConverter Instancia = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double ancho || double.IsNaN(ancho))
                return Visibility.Visible;

            double umbral = 700;
            if (parameter != null &&
                double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                umbral = p;

            return ancho >= umbral ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
