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
/// Responsividad: <see cref="MinColumnWidth"/> **reduce la cantidad de
/// columnas** cuando no hay espacio (4 → 3 → 2 → 1), en vez de encoger las
/// existentes hasta tapar el texto.
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
    /// Ancho mínimo deseable por columna. Si no entran <see cref="Columns"/>
    /// columnas de este ancho, el panel usa menos columnas.
    /// </summary>
    public static readonly DependencyProperty MinColumnWidthProperty =
        DependencyProperty.Register(
            nameof(MinColumnWidth), typeof(double), typeof(SpanningGridPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Cuántas columnas ocupa el hijo. Se recorta al total vigente.</summary>
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
        var rejilla = CalcularRejilla(disponible.Width);
        double alto = Recorrer(rejilla, medir: true, arreglar: false);
        return new Size(rejilla.AnchoTotal, alto);
    }

    protected override Size ArrangeOverride(Size final)
    {
        // MISMA función pura que MeasureOverride, y NO se vuelve a medir acá.
        //
        // Medir dentro de ArrangeOverride con una restricción distinta a la de
        // la medición hace que WPF dispare OnChildDesiredSizeChanged →
        // InvalidateMeasure → otra pasada de layout. Si además measure y arrange
        // reciben anchos distintos de forma sistemática (por ejemplo con scroll
        // horizontal habilitado, donde la medición llega con ancho infinito),
        // el layout NUNCA converge y la aplicación se traba.
        Recorrer(CalcularRejilla(final.Width), medir: false, arreglar: true);
        return final;
    }

    private readonly record struct Rejilla(int Columnas, double AnchoColumna, double AnchoTotal);

    /// <summary>
    /// Decide cuántas columnas entran y qué ancho tiene cada una. Es una función
    /// pura del ancho disponible: medir y arreglar con el mismo ancho dan
    /// exactamente el mismo resultado, que es lo que garantiza la convergencia.
    /// </summary>
    private Rejilla CalcularRejilla(double anchoDisponible)
    {
        int tope = Math.Max(1, Columns);
        double gap = ColumnSpacing;
        double minimo = Math.Max(0, MinColumnWidth);

        // Sin restricción de ancho no hay nada que repartir: se usa el tope con
        // el ancho mínimo, que es el tamaño natural del panel.
        if (double.IsInfinity(anchoDisponible) || double.IsNaN(anchoDisponible) || anchoDisponible <= 0)
        {
            double natural = minimo;
            return new Rejilla(tope, natural, natural * tope + gap * (tope - 1));
        }

        int columnas = tope;
        if (minimo > 0)
        {
            // Cuántas columnas de ancho mínimo entran en el espacio disponible.
            int caben = (int)Math.Floor((anchoDisponible + gap) / (minimo + gap));
            columnas = Math.Clamp(caben, 1, tope);
        }

        double anchoColumna = Math.Max(0, (anchoDisponible - gap * (columnas - 1)) / columnas);
        return new Rejilla(columnas, anchoColumna, anchoDisponible);
    }

    /// <summary>
    /// Empaquetado voraz: recorre los hijos en orden, los va poniendo en la fila
    /// actual mientras quepan y baja de fila cuando no. Una sola pasada sirve
    /// para medir y para arreglar, así el layout nunca se desincroniza.
    /// </summary>
    private double Recorrer(Rejilla rejilla, bool medir, bool arreglar)
    {
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

            int span = Math.Clamp(GetColumnSpan(hijo), 1, rejilla.Columnas);
            double anchoHijo = rejilla.AnchoColumna * span + gapCol * (span - 1);

            if (columnaActual + span > rejilla.Columnas && columnaActual > 0)
            {
                if (arreglar)
                    ArreglarFila(hijos, inicioFila, i, rejilla, gapCol, y, altoFila);
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
            ArreglarFila(hijos, inicioFila, hijos.Count, rejilla, gapCol, y, altoFila);

        return altoFila > 0 ? y + altoFila : Math.Max(0, y - gapFila);
    }

    private static void ArreglarFila(
        UIElementCollection hijos, int desde, int hasta,
        Rejilla rejilla, double gapCol, double y, double altoFila)
    {
        double x = 0;
        int columnaActual = 0;

        for (int i = desde; i < hasta; i++)
        {
            var hijo = hijos[i];
            if (hijo.Visibility == Visibility.Collapsed)
                continue;

            int span = Math.Clamp(GetColumnSpan(hijo), 1, rejilla.Columnas);
            if (columnaActual + span > rejilla.Columnas && columnaActual > 0)
                continue;

            double anchoHijo = rejilla.AnchoColumna * span + gapCol * (span - 1);
            // Alto de fila completo: los hijos de una misma fila quedan parejos
            // (el equivalente de align-items: stretch).
            hijo.Arrange(new Rect(x, y, anchoHijo, altoFila));

            x += anchoHijo + gapCol;
            columnaActual += span;
        }
    }
}
