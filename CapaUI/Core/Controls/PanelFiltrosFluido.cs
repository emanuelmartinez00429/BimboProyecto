using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CapaUI.Core.Controls;

/// <summary>
/// Panel para barras de filtros: mantiene todos los grupos en una sola línea
/// mientras entren y, cuando dejan de entrar, los reparte en varias líneas
/// equilibradas, dándole a los hijos flexibles de cada línea el ancho que sobra.
/// </summary>
/// <remarks>
/// <para>
/// Ninguno de los paneles de WPF hace las dos cosas a la vez: <c>WrapPanel</c>
/// envuelve pero deja el sobrante como espacio muerto a la derecha (los grupos
/// tienen anchos naturales distintos, así que corta en puntos arbitrarios y la
/// barra queda escalonada); <c>UniformGrid</c> reparte el ancho en partes
/// iguales pero nunca cambia de cantidad de columnas, así que no envuelve y a
/// ventana angosta recorta el contenido.
/// </para>
/// <para>
/// El reparto va por <see cref="PesoProperty"/>, al estilo de las columnas "*"
/// de un <c>Grid</c>: un hijo con peso 0 conserva su ancho natural (las
/// pastillas segmentadas, que no tienen por qué estirarse) y los de peso &gt; 0
/// se reparten el sobrante en proporción. El ancho natural sale de medir sin
/// restricción, así que un hijo con <c>MinWidth</c> nunca se achica por debajo
/// de donde su texto deja de leerse: llegado ese punto el panel prefiere bajarlo
/// de línea.
/// </para>
/// <para><b>Por qué equilibra en vez de llenar de a una línea.</b> Llenar
/// codiciosamente deja lo que sobra en la última línea, y un único combo ahí se
/// come todo el ancho de la barra. Por eso, una vez sabida la <i>cantidad
/// mínima</i> de líneas necesarias, el panel prueba todos los cortes posibles
/// con esa cantidad y se queda con el que minimiza el ancho del hijo flexible
/// más ancho. Con la barra de Productos eso produce la progresión natural:
/// todo en una línea → pastillas arriba y los cuatro combos abajo en cuartos →
/// pastillas, dos combos y dos combos. Una línea sin hijos flexibles (la de las
/// pastillas solas) no puntúa: su sobrante queda como margen a la derecha, que
/// es como se ve normalmente una barra de herramientas.</para>
/// </remarks>
public class PanelFiltrosFluido : Panel
{
    // ── Peso (adjunta) ────────────────────────────────────────────────

    /// <summary>
    /// Cuánto del sobrante de su línea absorbe este hijo, en proporción al peso
    /// total de la línea. 0 (por defecto) = ancho natural, no se estira.
    /// </summary>
    public static readonly DependencyProperty PesoProperty =
        DependencyProperty.RegisterAttached(
            "Peso", typeof(double), typeof(PanelFiltrosFluido),
            new FrameworkPropertyMetadata(0d,
                FrameworkPropertyMetadataOptions.AffectsParentMeasure |
                FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void   SetPeso(UIElement elemento, double valor) => elemento.SetValue(PesoProperty, valor);
    public static double GetPeso(UIElement elemento) => (double)elemento.GetValue(PesoProperty);

    // ── Espaciado ─────────────────────────────────────────────────────

    public static readonly DependencyProperty EspacioHorizontalProperty =
        DependencyProperty.Register(
            nameof(EspacioHorizontal), typeof(double), typeof(PanelFiltrosFluido),
            new FrameworkPropertyMetadata(14d,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsArrange));

    public static readonly DependencyProperty EspacioVerticalProperty =
        DependencyProperty.Register(
            nameof(EspacioVertical), typeof(double), typeof(PanelFiltrosFluido),
            new FrameworkPropertyMetadata(12d,
                FrameworkPropertyMetadataOptions.AffectsMeasure |
                FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>Separación entre grupos de una misma línea.</summary>
    public double EspacioHorizontal
    {
        get => (double)GetValue(EspacioHorizontalProperty);
        set => SetValue(EspacioHorizontalProperty, value);
    }

    /// <summary>Separación entre líneas cuando la barra envuelve.</summary>
    public double EspacioVertical
    {
        get => (double)GetValue(EspacioVerticalProperty);
        set => SetValue(EspacioVerticalProperty, value);
    }

    // ── Estado de layout ──────────────────────────────────────────────

    private sealed class Linea
    {
        public readonly List<UIElement> Hijos = new();
        /// <summary>Suma de anchos naturales más los espacios intermedios.</summary>
        public double AnchoNatural;
        public double Alto;
    }

    /// <summary>
    /// Tamaño natural de cada hijo, capturado en la medición sin restricción.
    /// No se puede leer <c>DesiredSize</c> en <see cref="ArrangeOverride"/>
    /// porque ahí los hijos se re-miden al ancho ya estirado y ese valor pisa el
    /// natural; como WPF puede correr un pase de arrange sin measure previo, el
    /// reparto siguiente leería anchos inflados y el corte de línea se
    /// degradaría solo hasta dejar un grupo por línea.
    /// </summary>
    private readonly Dictionary<UIElement, Size> _naturales = new();

    /// <summary>
    /// Tope de cortes a evaluar al equilibrar. Con la cantidad de grupos de una
    /// barra de filtros nunca se alcanza; está para que el costo no explote si
    /// alguna pantalla llega con muchos más hijos, en cuyo caso se cae al
    /// reparto codicioso, que siempre es válido.
    /// </summary>
    private const int MaxCombinaciones = 500;

    // ── Medición y arreglo ────────────────────────────────────────────

    protected override Size MeasureOverride(Size disponible)
    {
        var sinLimite = new Size(double.PositiveInfinity, double.PositiveInfinity);

        _naturales.Clear();
        foreach (UIElement hijo in InternalChildren)
        {
            hijo.Measure(sinLimite);
            _naturales[hijo] = hijo.DesiredSize;
        }

        var lineas = RepartirEnLineas(disponible.Width);

        double alto  = 0;
        double ancho = 0;
        foreach (var linea in lineas)
        {
            alto  += linea.Alto;
            ancho  = Math.Max(ancho, linea.AnchoNatural);
        }
        if (lineas.Count > 1)
            alto += EspacioVertical * (lineas.Count - 1);

        // Con ancho acotado se devuelve el disponible: el panel ocupa todo el
        // renglón y los pesos reparten el sobrante en Arrange.
        return new Size(double.IsInfinity(disponible.Width) ? ancho : disponible.Width, alto);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var lineas = RepartirEnLineas(final.Width);

        double y = 0;
        foreach (var linea in lineas)
        {
            double sobrante  = Math.Max(0, final.Width - linea.AnchoNatural);
            double pesoTotal = 0;
            foreach (var hijo in linea.Hijos)
                pesoTotal += GetPeso(hijo);

            double x = 0;
            foreach (var hijo in linea.Hijos)
            {
                double ancho = Natural(hijo).Width;
                if (pesoTotal > 0)
                    ancho += sobrante * (GetPeso(hijo) / pesoTotal);

                // Segunda medición al ancho definitivo: sin esto el contenido
                // interno (la columna "*" que lleva al combo) se acomoda contra
                // el ancho natural y no llena la celda estirada.
                hijo.Measure(new Size(ancho, linea.Alto));
                hijo.Arrange(new Rect(x, y, ancho, linea.Alto));

                x += ancho + EspacioHorizontal;
            }

            y += linea.Alto + EspacioVertical;
        }

        return final;
    }

    // ── Reparto en líneas ─────────────────────────────────────────────

    /// <summary>
    /// Arma las líneas: primero el corte codicioso (que da la cantidad mínima
    /// posible) y después el equilibrado dentro de esa misma cantidad.
    /// </summary>
    private List<Linea> RepartirEnLineas(double anchoDisponible)
    {
        var visibles = new List<UIElement>();
        foreach (UIElement hijo in InternalChildren)
            if (hijo.Visibility != Visibility.Collapsed)
                visibles.Add(hijo);

        var codicioso = EmpaquetarCodicioso(visibles, anchoDisponible);

        // Una sola línea ya es óptima: no hay nada que equilibrar.
        if (codicioso.Count <= 1) return codicioso;

        // El reparto codicioso da la cantidad MÍNIMA de líneas posible
        // respetando el orden; equilibrar dentro de esa cantidad reacomoda los
        // grupos sin agregar líneas de más.
        return Equilibrar(visibles, anchoDisponible, codicioso.Count) ?? codicioso;
    }

    /// <summary>
    /// Llena cada línea hasta que el grupo siguiente no entra. Un hijo que por
    /// sí solo excede el ancho igual abre línea propia: es preferible que se
    /// recorte él a que arrastre a los anteriores.
    /// </summary>
    private List<Linea> EmpaquetarCodicioso(List<UIElement> visibles, double anchoDisponible)
    {
        var lineas = new List<Linea>();
        var actual = new Linea();

        foreach (var hijo in visibles)
        {
            var natural = Natural(hijo);
            double conEsteHijo = actual.Hijos.Count == 0
                ? natural.Width
                : actual.AnchoNatural + EspacioHorizontal + natural.Width;

            if (actual.Hijos.Count > 0 && conEsteHijo > anchoDisponible)
            {
                lineas.Add(actual);
                actual      = new Linea();
                conEsteHijo = natural.Width;
            }

            actual.Hijos.Add(hijo);
            actual.AnchoNatural = conEsteHijo;
            actual.Alto         = Math.Max(actual.Alto, natural.Height);
        }

        if (actual.Hijos.Count > 0) lineas.Add(actual);
        return lineas;
    }

    /// <summary>
    /// Prueba todos los cortes que producen exactamente <paramref name="lineas"/>
    /// líneas y devuelve el que deja más angosto al hijo flexible más ancho.
    /// Devuelve <c>null</c> si ningún corte es viable o si hay demasiados que
    /// evaluar — el llamador se queda entonces con el reparto codicioso.
    /// </summary>
    private List<Linea>? Equilibrar(List<UIElement> visibles, double ancho, int lineas)
    {
        int posiciones = visibles.Count - 1;
        int cortesNecesarios = lineas - 1;
        if (cortesNecesarios <= 0 || cortesNecesarios > posiciones) return null;
        if (Combinaciones(posiciones, cortesNecesarios) > MaxCombinaciones) return null;

        List<Linea>? mejor = null;
        double mejorPuntaje = double.PositiveInfinity;
        var cortes = new int[cortesNecesarios];

        Explorar(0, 1);
        return mejor;

        void Explorar(int indice, int desde)
        {
            if (indice == cortesNecesarios)
            {
                var candidato = Construir(visibles, cortes, ancho, out double puntaje);
                if (candidato is not null && puntaje < mejorPuntaje)
                {
                    mejor        = candidato;
                    mejorPuntaje = puntaje;
                }
                return;
            }

            // Deja lugar para los cortes que faltan.
            int ultimaPosicion = visibles.Count - (cortesNecesarios - indice);
            for (int p = desde; p <= ultimaPosicion; p++)
            {
                cortes[indice] = p;
                Explorar(indice + 1, p + 1);
            }
        }
    }

    /// <summary>
    /// Arma las líneas a partir de las posiciones de corte y calcula el puntaje:
    /// el ancho del hijo flexible más ancho una vez repartido el sobrante. Las
    /// líneas sin hijos flexibles no puntúan — su sobrante queda como margen a
    /// la derecha, que es lo esperable en una barra de herramientas. Devuelve
    /// <c>null</c> si alguna línea no entra en el ancho disponible.
    /// </summary>
    private List<Linea>? Construir(List<UIElement> visibles, int[] cortes, double ancho, out double puntaje)
    {
        var lineas = new List<Linea>(cortes.Length + 1);
        puntaje    = 0;

        int inicio = 0;
        for (int i = 0; i <= cortes.Length; i++)
        {
            int fin = i < cortes.Length ? cortes[i] : visibles.Count;

            var linea = new Linea();
            for (int j = inicio; j < fin; j++)
            {
                var hijo    = visibles[j];
                var natural = Natural(hijo);

                linea.AnchoNatural += linea.Hijos.Count == 0
                    ? natural.Width
                    : EspacioHorizontal + natural.Width;
                linea.Alto = Math.Max(linea.Alto, natural.Height);
                linea.Hijos.Add(hijo);
            }

            if (linea.AnchoNatural > ancho) return null;   // no entra: corte inviable

            double pesoTotal = 0;
            foreach (var hijo in linea.Hijos)
                pesoTotal += GetPeso(hijo);

            if (pesoTotal > 0)
            {
                double sobrante = Math.Max(0, ancho - linea.AnchoNatural);
                foreach (var hijo in linea.Hijos)
                {
                    double peso = GetPeso(hijo);
                    if (peso <= 0) continue;
                    puntaje = Math.Max(puntaje, Natural(hijo).Width + sobrante * (peso / pesoTotal));
                }
            }

            lineas.Add(linea);
            inicio = fin;
        }

        return lineas;
    }

    private static double Combinaciones(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        double total = 1;
        for (int i = 1; i <= k; i++)
            total = total * (n - k + i) / i;
        return total;
    }

    /// <summary>
    /// Tamaño natural cacheado. El fallback a <c>DesiredSize</c> cubre el caso
    /// de un hijo agregado entre la medición y el arreglo.
    /// </summary>
    private Size Natural(UIElement hijo) =>
        _naturales.TryGetValue(hijo, out var natural) ? natural : hijo.DesiredSize;
}
