namespace CapaUI.Core.Controls;

/// <summary>
/// Ventana de páginas compartida por los formularios paginados.
///
/// El mismo algoritmo estaba copiado literal en varios code-behind
/// (SelectorProductosModal, CategoriasView, ProductosView…), difiriendo solo en
/// detalles de estilo del separador. Vive acá una sola vez.
/// </summary>
public static class Paginacion
{
    /// <summary>Separador de elipsis dentro de la secuencia de páginas.</summary>
    public const int Elipsis = -1;

    /// <summary>
    /// Páginas a mostrar alrededor de la actual. Devuelve <see cref="Elipsis"/>
    /// donde corresponde un "…".
    /// </summary>
    public static IEnumerable<int> Calcular(int current, int total)
    {
        if (total <= 7) return Enumerable.Range(1, total);

        var pages = new List<int> { 1 };
        if (current > 3) pages.Add(Elipsis);
        for (int i = Math.Max(2, current - 1); i <= Math.Min(total - 1, current + 1); i++)
            pages.Add(i);
        if (current < total - 2) pages.Add(Elipsis);
        pages.Add(total);
        return pages;
    }
}
