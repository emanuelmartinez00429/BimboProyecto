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

    /// <summary>
    /// Piso de ancho por columna. Hasta acá el panel es fluido; por debajo deja
    /// de encoger y reporta un ancho mayor al disponible, lo que hace aparecer
    /// la barra horizontal del <see cref="System.Windows.Controls.ScrollViewer"/>
    /// contenedor en vez de aplastar el contenido hasta tapar el texto.
    /// </summary>
    public static readonly DependencyProperty MinColumnWidthProperty =
        DependencyProperty.Register(
            nameof(MinColumnWidth), typeof(double), typeof(SpanningGridPanel),
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

    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    public static int GetColumnSpan(DependencyObject elemento) =>
        (int)elemento.GetValue(ColumnSpanProperty);

    public static void SetColumnSpan(DependencyObject elemento, int valor) =>
        elemento.SetValue(ColumnSpanProperty, valor);

    protected override Size MeasureOverride(Size disponible)
    {
        int columnas = Math.Max(1, Columns);
        double anchoColumna = CalcularAnchoColumna(disponible.Width);
        double alto = Distribuir(anchoColumna, arreglar: false);

        // Se reporta el ancho que el panel NECESITA, no el que le ofrecieron.
        // Cuando supera al disponible, el ScrollViewer contenedor lo detecta y
        // saca la barra horizontal en lugar de recortar el contenido.
        double anchoTotal = anchoColumna * columnas + ColumnSpacing * (columnas - 1);
        return new Size(anchoTotal, alto);
    }

    protected override Size ArrangeOverride(Size final)
    {
        Distribuir(CalcularAnchoColumna(final.Width), arreglar: true);
        return final;
    }

    /// <summary>
    /// Reparte el ancho entre las columnas, sin bajar de <see cref="MinColumnWidth"/>.
    /// Ancho infinito (típico dentro de un ScrollViewer con scroll horizontal
    /// habilitado) significa "no hay restricción": se usa el mínimo.
    /// </summary>
    private double CalcularAnchoColumna(double anchoDisponible)
    {
        double minimo = Math.Max(0, MinColumnWidth);

        if (double.IsInfinity(anchoDisponible) || double.IsNaN(anchoDisponible))
            return minimo;

        int columnas = Math.Max(1, Columns);
        double repartido = (anchoDisponible - ColumnSpacing * (columnas - 1)) / columnas;
        return Math.Max(minimo, Math.Max(0, repartido));
    }

    /// <summary>
    /// Empaquetado voraz: recorre los hijos en orden, los va poniendo en la fila
    /// actual mientras quepan y baja de fila cuando no. Una sola pasada sirve
    /// para medir y para arreglar, así el layout nunca se desincroniza.
    ///
    /// Mide siempre, también en la pasada de arreglo: dentro de un ScrollViewer
    /// la medición llega con ancho infinito y el arreglo con el ancho real, así
    /// que hay que volver a medir o el texto quedaría recortado según el ancho
    /// equivocado. Medir con la misma restricción es un no-op para WPF.
    /// </summary>
    private double Distribuir(double anchoColumna, bool arreglar)
    {
        int columnas = Math.Max(1, Columns);
        double gapCol = ColumnSpacing;
        double gapFila = RowSpacing;

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
