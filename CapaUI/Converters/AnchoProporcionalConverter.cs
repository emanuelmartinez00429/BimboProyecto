using System;
using System.Globalization;
using System.Windows.Data;

namespace CapaUI.Converters
{
    /// Ancho de una barra de progreso como fraccion del ancho de su contenedor:
    /// <c>values = [ escala 0..1, ancho del contenedor ]</c>.
    ///
    /// Existe porque un <c>ScaleTransform</c> es un <c>Freezable</c> fuera del arbol
    /// visual: no hereda <c>DataContext</c>, asi que <c>ScaleX="{Binding Escala}"</c>
    /// nunca resuelve y WPF lo reporta como
    /// <c>Cannot find governing FrameworkElement</c> — la barra queda siempre en
    /// ScaleX=1 (llena). Bindear el <c>Width</c> de un <c>Border</c>, que si es
    /// FrameworkElement, evita el problema en vez de rodearlo.
    public sealed class AnchoProporcionalConverter : IMultiValueConverter
    {
        /// <summary>
        /// Instancia compartida para usar desde XAML con
        /// <c>Converter={x:Static conv:AnchoProporcionalConverter.Instancia}</c>,
        /// segun la convencion del proyecto (ver <c>RestarMargenConverter</c>).
        /// </summary>
        public static readonly AnchoProporcionalConverter Instancia = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values is not [double escala, double anchoContenedor]) return 0d;
            if (double.IsNaN(anchoContenedor) || anchoContenedor <= 0) return 0d;

            escala = Math.Max(0, Math.Min(1, escala));
            return Math.Round(anchoContenedor * escala, 2);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
