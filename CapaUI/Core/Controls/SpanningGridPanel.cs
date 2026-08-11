using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Core.Controls;

/// <summary>
/// Panel de columnas uniformes que soporta que un hijo ocupe varias columnas,
/// el equivalente WPF de <c>grid-template-columns: repeat(N,1fr)</c> +
/// <c>grid-column: span 2</c>.
///
/// Por qué existe: <see cref="UniformGrid"/> no permite span y fuerza a que
/// TODAS las celdas midan lo mismo; <see cref="WrapPanel"/> no estira los
/// elementos de una fila a la misma altura. Acá cada fila calcula su propia
/// altura y todos sus hijos se estiran a ella, que es lo que pide el diseño.
///
/// Sin estado entre pasadas y sin bindings por hijo: el costo es O(hijos).
/// </summary>
public class SpanningGridPanel : Panel
{
    public static readonly DependencyProperty ColumnsProperty =
        DependencyProperty.Register(
            nameof(Columns), typeof(int), typeof(SpanningGridPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsMeasure),
            valor => valor is int n && n >= 1);

    public static readonly DependencyProperty ColumnSpacingProperty =
        DependencyProperty.Register(
            nameof(ColumnSpacing), typeof(double), typeof(SpanningGridPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowSpacingProperty =
        DependencyProperty.Register(
            nameof(RowSpacing), typeof(double), typeof(SpanningGridPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Cuántas columnas ocupa el hijo. Se recorta al total de columnas.</summary>
    public static readonly DependencyProperty ColumnSpanProperty =
        DependencyProperty.RegisterAttached(
            "ColumnSpan", typeof(int), typeof(SpanningGridPanel),
            new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public static int GetColumnSpan(DependencyObject elemento) =>
        (int)elemento.GetValue(ColumnSpanProperty);

    public static void SetColumnSpan(DependencyObject elemento, int valor) =>
        elemento.SetValue(ColumnSpanProperty, valor);

    protected override Size MeasureOverride(Size disponible)
    {
        double ancho = double.IsInfinity(disponible.Width) ? 0 : disponible.Width;
        return new Size(ancho, Distribuir(ancho, medir: true, arreglar: false));
    }

    protected override Size ArrangeOverride(Size final)
    {
        Distribuir(final.Width, medir: false, arreglar: true);
        return final;
    }

    /// <summary>
    /// Empaquetado voraz: recorre los hijos en orden, los va poniendo en la fila
    /// actual mientras quepan y baja de fila cuando no. Una sola pasada sirve
    /// para medir y para arreglar, así el layout nunca se desincroniza.
    /// </summary>
    private double Distribuir(double anchoDisponible, bool medir, bool arreglar)
    {
        int columnas = Math.Max(1, Columns);
        double gapCol = ColumnSpacing;
        double gapFila = RowSpacing;
        double anchoColumna = Math.Max(0, (anchoDisponible - gapCol * (columnas - 1)) / columnas);

        double y = 0;
        int columnaActual = 0;
        double altoFila = 0;
        int inicioFila = 0;

        var hijos = InternalChildren;

        for (int i = 0; i < hijos.Count; i++)
        {
            var hijo = hijos[i];
            if (hijo.Visibility == Visibility.Collapsed)
                continue;

            int span = Math.Clamp(GetColumnSpan(hijo), 1, columnas);
            double anchoHijo = anchoColumna * span + gapCol * (span - 1);

            if (columnaActual + span > columnas && columnaActual > 0)
            {
                if (arreglar)
                    ArreglarFila(hijos, inicioFila, i, anchoColumna, gapCol, y, altoFila);
                y += altoFila + gapFila;
                columnaActual = 0;
                altoFila = 0;
                inicioFila = i;
            }

            if (medir)
                hijo.Measure(new Size(anchoHijo, double.PositiveInfinity));

            altoFila = Math.Max(altoFila, hijo.DesiredSize.Height);
            columnaActual += span;
        }

        if (arreglar)
            ArreglarFila(hijos, inicioFila, hijos.Count, anchoColumna, gapCol, y, altoFila);

        return altoFila > 0 ? y + altoFila : Math.Max(0, y - gapFila);
    }

    private void ArreglarFila(
        UIElementCollection hijos, int desde, int hasta,
        double anchoColumna, double gapCol, double y, double altoFila)
    {
        int columnas = Math.Max(1, Columns);
        double x = 0;
        int columnaActual = 0;

        for (int i = desde; i < hasta; i++)
        {
            var hijo = hijos[i];
            if (hijo.Visibility == Visibility.Collapsed)
                continue;

            int span = Math.Clamp(GetColumnSpan(hijo), 1, columnas);
            if (columnaActual + span > columnas && columnaActual > 0)
                return;

            double anchoHijo = anchoColumna * span + gapCol * (span - 1);
            // Alto de fila completo: los hijos de una misma fila quedan parejos
            // (el equivalente de align-items: stretch).
            hijo.Arrange(new Rect(x, y, anchoHijo, altoFila));

            x += anchoHijo + gapCol;
            columnaActual += span;
        }
    }
}
