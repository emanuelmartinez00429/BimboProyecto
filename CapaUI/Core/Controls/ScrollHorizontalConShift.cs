using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapaUI.Core.Controls;

/// <summary>
/// Habilita scroll horizontal con <c>Shift + rueda</c> (o Shift + gesto de dos
/// dedos del trackpad) sobre un <see cref="DataGrid"/>.
///
/// WPF no hace esto solo: <c>ScrollViewer.OnMouseWheel</c> siempre scrollea
/// vertical, sin mirar <see cref="Keyboard.Modifiers"/> — el "Shift+rueda = scroll
/// horizontal" que dan por sentado Excel o los navegadores es una convención de
/// cada app, no algo que trae el framework. Hay que armarlo a mano.
/// </summary>
public static class ScrollHorizontalConShift
{
    /// <summary>
    /// El <see cref="ScrollViewer"/> es parte fija del template por defecto de
    /// <see cref="DataGrid"/> (no cambia entre pantallas), así que alcanza con
    /// ubicarlo una vez y cachearlo — no hay que caminar el árbol visual en cada
    /// evento de rueda.
    /// </summary>
    public static void Habilitar(DataGrid grid)
    {
        ScrollViewer? scroll = null;

        grid.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Shift) return;

            scroll ??= BuscarScrollViewer(grid);
            if (scroll is null) return;

            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        };
    }

    private static ScrollViewer? BuscarScrollViewer(DependencyObject raiz)
    {
        int hijos = VisualTreeHelper.GetChildrenCount(raiz);
        for (int i = 0; i < hijos; i++)
        {
            var hijo = VisualTreeHelper.GetChild(raiz, i);
            if (hijo is ScrollViewer sv) return sv;

            var encontrado = BuscarScrollViewer(hijo);
            if (encontrado is not null) return encontrado;
        }
        return null;
    }
}
