using System.Windows;
using System.Windows.Data;
using CapaUI.Converters;

namespace CapaUI.Core
{
    /// <summary>
    /// Ata el <c>MaxWidth</c>/<c>MaxHeight</c> de un modal al tamaño de su overlay
    /// contenedor, dejando un margen fijo por lado.
    /// <para/>
    /// Reemplaza el binding que los modales llevaban en su elemento raíz
    /// (<c>MaxWidth="{Binding ActualWidth, RelativeSource={RelativeSource AncestorType=Border}, ...}"</c>).
    /// Esa búsqueda de ancestro se escapa del control: en el diseñador de Visual Studio
    /// el modal cuelga del árbol visual del propio VS, engancha un <c>Border</c> de su
    /// infraestructura, ese Border mide 0 en el primer measure, el converter devuelve
    /// <c>max(0, 0-48) = 0</c> y con <c>MaxWidth=0</c> el modal colapsa a 0×0 — el lienzo
    /// muestra el artboard vacío. Atado acá contra el overlay por referencia directa no
    /// hay ancestro que buscar, y el tamaño lo decide quien hospeda, que es el contrato
    /// de layout natural de WPF. Ver ADR-028 y <c>PesajeView.LimitarAlOverlay</c>.
    /// </summary>
    public static class ModalLayout
    {
        public static void LimitarAlOverlay(FrameworkElement modal, FrameworkElement overlay, double margen = 48)
        {
            Atar(FrameworkElement.MaxWidthProperty,  nameof(FrameworkElement.ActualWidth));
            Atar(FrameworkElement.MaxHeightProperty, nameof(FrameworkElement.ActualHeight));

            void Atar(DependencyProperty destino, string propiedadDelOverlay) =>
                modal.SetBinding(destino, new Binding(propiedadDelOverlay)
                {
                    Source             = overlay,
                    Converter          = RestarMargenConverter.Instancia,
                    ConverterParameter = margen,
                });
        }
    }
}
