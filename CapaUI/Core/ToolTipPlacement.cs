using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Core
{
    /// <summary>
    /// Baja todos los ToolTip lo justo para que el puntero del mouse no los tape.
    /// <para/>
    /// WPF ubica el tooltip (<c>PlacementMode.Mouse</c>) unos 17 px por debajo del punto
    /// caliente del mouse, sin mirar cuánto mide el cursor que se está dibujando. Con la
    /// flecha estándar alcanza, pero la mano de los botones mide 32 px, así que pisaba el
    /// borde superior del tooltip. Medido con hover real: sin ajuste el tooltip queda a
    /// 17 px del mouse; con este desfase, a 34 px (2 px de aire bajo la mano).
    /// <para/>
    /// El valor es fijo a propósito: la mano que dibuja la app mide 32 px aunque Windows
    /// tenga el cursor agrandado (<c>CursorBaseSize</c>), y usar ese ajuste del sistema
    /// dejaba el tooltip demasiado lejos.
    /// <para/>
    /// Se aplica como valor por defecto de <see cref="ToolTipService.VerticalOffsetProperty"/>
    /// para todo <see cref="FrameworkElement"/>: cubre también los tooltips de texto, que WPF
    /// crea solo, y cualquier control puede seguir fijando su propio
    /// <c>ToolTipService.VerticalOffset</c>.
    /// </summary>
    public static class ToolTipPlacement
    {
        /// <summary>Distancia medida entre el mouse y el borde superior del tooltip sin ajuste.</summary>
        private const double SeparacionPorDefectoWpf = 17;

        /// <summary>Alto de la mano (y de la flecha) que dibuja la app.</summary>
        private const double AltoCursor = 32;

        /// <summary>Aire entre la punta inferior del cursor y el tooltip.</summary>
        private const double Margen = 2;

        /// <summary>Llamar una sola vez, al arrancar, antes de que exista cualquier ventana.</summary>
        public static void Configurar()
        {
            const double desfase = AltoCursor - SeparacionPorDefectoWpf + Margen;

            ToolTipService.VerticalOffsetProperty.OverrideMetadata(
                typeof(FrameworkElement), new FrameworkPropertyMetadata(desfase));
        }
    }
}
