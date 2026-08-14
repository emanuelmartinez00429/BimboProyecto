using System.Globalization;
using System.Text;

namespace CapaAplicacion.Common;

/// <summary>
/// Normalización de texto para búsquedas: minúsculas y sin tildes, de modo que
/// "azucar" encuentre "AZÚCAR" y viceversa.
/// </summary>
/// <remarks>
/// <para>
/// Tiene que coincidir con lo que hace la función <c>public.sin_tildes()</c> de
/// la base, que es la que alimenta las columnas generadas de búsqueda. Si las
/// dos normalizaciones divergen, el término que manda el cliente deja de
/// corresponderse con lo que hay guardado y la búsqueda devuelve de menos sin
/// dar ningún error — por eso conviene tocar las dos juntas.
/// </para>
/// <para>
/// La descomposición Unicode (FormD) separa cada letra de su tilde y después se
/// descartan las marcas: así "á"→"a" y "ñ"→"n", igual que <c>unaccent</c> en
/// Postgres.
/// </para>
/// </remarks>
public static class TextoBusqueda
{
    /// <summary>Minúsculas y sin tildes. Nunca devuelve null.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;

        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var limpio       = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                limpio.Append(caracter);
        }

        return limpio.ToString()
                     .Normalize(NormalizationForm.FormC)
                     .ToLowerInvariant();
    }

    /// <summary>
    /// "Contiene" insensible a mayúsculas y tildes. Es la versión en memoria de
    /// lo que hace el ILIKE contra las columnas de búsqueda del servidor.
    /// </summary>
    public static bool Contiene(string? texto, string? termino)
    {
        var aguja = Normalizar(termino);
        return aguja.Length == 0 || Normalizar(texto).Contains(aguja, StringComparison.Ordinal);
    }
}
