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
        /// <summary>
        /// Instancia compartida para usar desde XAML con
        /// <c>Converter={x:Static conv:RestarMargenConverter.Instancia}</c>.
        /// <para/>
        /// Existe porque el converter se usa en el <b>atributo del elemento raiz</b> de los
        /// modales (MaxWidth/MaxHeight). Ahi un <c>{StaticResource}</c> no sirve: los
        /// atributos del raiz se aplican ANTES de que se pueble su propio
        /// <c>&lt;UserControl.Resources&gt;</c>, asi que buscar la clave localmente es una
        /// referencia hacia adelante y WPF la resuelve solo si el recurso ya esta en un
        /// ancestro — en la app lo esta (App.xaml), pero el disenador de Visual Studio no
        /// instancia App y ahi el parseo revienta y el lienzo queda en blanco.
        /// <c>{x:Static}</c> lo resuelve contra el tipo CLR, sin diccionario de recursos:
        /// funciona igual en runtime, en el disenador y en pruebas.
        /// </summary>
        public static readonly RestarMargenConverter Instancia = new();

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
