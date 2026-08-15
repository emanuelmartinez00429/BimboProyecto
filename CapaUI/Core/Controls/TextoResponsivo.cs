using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapaUI.Core.Controls;

/// <summary>
/// Comportamiento adjunto para los campos de los modales: cuando el modal se
/// achica (responsive) y un valor deja de entrar en su campo, muestra el
/// contenido completo por <see cref="ToolTip"/> — y, en campos de solo
/// lectura, además lo recorta visualmente con "…", igual que
/// <c>TextTrimming="CharacterEllipsis"</c> en un <see cref="TextBlock"/> (que
/// un <see cref="TextBox"/> no soporta nativamente).
/// </summary>
/// <remarks>
/// <para>
/// Se activa vía <see cref="ActivoProperty"/> en el <c>Style</c> compartido
/// de los campos de modal (<c>ModalInput</c> en <c>Styles.xaml</c>), disparado
/// por un <c>Trigger</c> sobre <c>IsReadOnly</c> — así cubre automáticamente
/// cualquier modal que use ese estilo, sin tocar cada campo uno por uno.
/// </para>
/// <para><b>Por qué el tratamiento es distinto según editable/solo-lectura:</b></para>
/// <list type="bullet">
/// <item>
/// <b>Solo lectura</b> (campos de catálogo elegidos por lupa: Fabricante,
/// Proveedor, Categoría…): el texto no es algo que el usuario esté tipeando,
/// así que es seguro reemplazar <see cref="TextBox.Text"/> por una versión
/// recortada — el valor real persistido vive en un campo `int? _idXxx`
/// aparte, nunca en este texto. Se guarda el original en
/// <see cref="TextoOriginalProperty"/> para poder recalcular si la ventana
/// vuelve a crecer.
/// </item>
/// <item>
/// <b>Editable</b> (Nombre, Código…): truncar <see cref="TextBox.Text"/>
/// mientras el usuario escribe le comería el input. Ahí solo se agrega el
/// <see cref="ToolTip"/> con el texto completo cuando no entra — el
/// <see cref="TextBox"/> ya se encarga de desplazarse horizontalmente por su
/// cuenta, que es el comportamiento esperable de un campo editable.
/// </item>
/// </list>
/// </remarks>
public static class TextoResponsivo
{
    public static readonly DependencyProperty ActivoProperty =
        DependencyProperty.RegisterAttached(
            "Activo", typeof(bool), typeof(TextoResponsivo),
            new PropertyMetadata(false, OnActivoChanged));

    public static void SetActivo(TextBox elemento, bool valor) => elemento.SetValue(ActivoProperty, valor);
    public static bool GetActivo(TextBox elemento) => (bool)elemento.GetValue(ActivoProperty);

    /// <summary>Texto sin recortar. Solo se usa en campos de solo lectura.</summary>
    private static readonly DependencyProperty TextoOriginalProperty =
        DependencyProperty.RegisterAttached("TextoOriginal", typeof(string), typeof(TextoResponsivo));

    /// <summary>Evita el bucle: al recortar, el propio recorte dispara TextChanged.</summary>
    private static readonly DependencyProperty AutoAjustandoProperty =
        DependencyProperty.RegisterAttached("AutoAjustando", typeof(bool), typeof(TextoResponsivo));

    private static void OnActivoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox caja) return;

        if ((bool)e.NewValue)
        {
            caja.TextChanged  += AlCambiarTexto;
            caja.SizeChanged  += AlCambiarTamaño;
            caja.Loaded       += AlCargar;
        }
        else
        {
            caja.TextChanged  -= AlCambiarTexto;
            caja.SizeChanged  -= AlCambiarTamaño;
            caja.Loaded       -= AlCargar;
        }
    }

    private static void AlCargar(object sender, RoutedEventArgs e) => Recalcular((TextBox)sender);

    private static void AlCambiarTamaño(object sender, SizeChangedEventArgs e) => Recalcular((TextBox)sender);

    private static void AlCambiarTexto(object sender, TextChangedEventArgs e)
    {
        var caja = (TextBox)sender;
        if ((bool)caja.GetValue(AutoAjustandoProperty)) return;   // eco de nuestro propio recorte

        if (caja.IsReadOnly)
        {
            // Nuevo valor asignado desde código (p. ej. al elegir un fabricante
            // por la lupa): pasa a ser el nuevo "original" y se vuelve a evaluar.
            caja.SetValue(TextoOriginalProperty, caja.Text);
        }

        Recalcular(caja);
    }

    private static void Recalcular(TextBox caja)
    {
        if (!caja.IsLoaded || caja.ActualWidth <= 0) return;

        string textoCompleto = caja.IsReadOnly
            ? (string)caja.GetValue(TextoOriginalProperty) ?? caja.Text
            : caja.Text;

        if (string.IsNullOrEmpty(textoCompleto)) { caja.ToolTip = null; return; }

        double anchoDisponible = caja.ActualWidth - caja.Padding.Left - caja.Padding.Right - 4; // margen de caret/borde
        double anchoTexto = MedirAncho(caja, textoCompleto);

        bool noEntra = anchoTexto > anchoDisponible;
        caja.ToolTip = noEntra ? textoCompleto : null;

        // El recorte visual con "…" solo aplica a campos de solo lectura — ver
        // remarks de la clase: en uno editable esto se comería lo que el
        // usuario está escribiendo.
        if (!caja.IsReadOnly) return;

        string textoFinal = noEntra ? RecortarAlAncho(caja, textoCompleto, anchoDisponible) : textoCompleto;
        if (caja.Text == textoFinal) return;

        caja.SetValue(AutoAjustandoProperty, true);
        try   { caja.Text = textoFinal; caja.CaretIndex = 0; }
        finally { caja.SetValue(AutoAjustandoProperty, false); }
    }

    /// <summary>Ancho real que ocupa <paramref name="texto"/> con la tipografía de la caja.</summary>
    private static double MedirAncho(TextBox caja, string texto)
    {
        var formateado = new FormattedText(
            texto,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(caja.FontFamily, caja.FontStyle, caja.FontWeight, caja.FontStretch),
            caja.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(caja).PixelsPerDip);
        return formateado.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// Búsqueda binaria del prefijo más largo de <paramref name="texto"/> que,
    /// con "…" agregado, entra en <paramref name="anchoDisponible"/>. Lineal
    /// carácter por carácter sería más simple pero más lento en textos largos;
    /// binaria alcanza en ~log(n) mediciones y esto corre en cada resize.
    /// </summary>
    private static string RecortarAlAncho(TextBox caja, string texto, double anchoDisponible)
    {
        const string puntos = "…";
        if (MedirAncho(caja, puntos) > anchoDisponible) return puntos;

        int bajo = 0, alto = texto.Length;
        while (bajo < alto)
        {
            int medio = (bajo + alto + 1) / 2;
            string candidato = texto[..medio] + puntos;
            if (MedirAncho(caja, candidato) <= anchoDisponible) bajo = medio;
            else                                                alto = medio - 1;
        }

        return bajo == 0 ? puntos : texto[..bajo] + puntos;
    }
}
