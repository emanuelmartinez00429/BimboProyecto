using System.Globalization;
using System.Text.RegularExpressions;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Convierte a número lo que tipeó una persona.
/// </summary>
/// <remarks>
/// <para>
/// Esto <b>no</b> es una regla de negocio y por eso no está en
/// <c>CapaDominio.Reglas</c>: depende del <see cref="CultureInfo.CurrentCulture"/>
/// del usuario, o sea de si escribe "1,5" o "1.5". El dominio declara que un
/// campo es <c>FormatoCampo.Decimal</c>; traducir el texto al número según la
/// configuración regional es responsabilidad de la capa que recibe el tecleo.
/// </para>
/// <para>
/// ⚠️ Los modales de Pesaje parsean con <c>InvariantCulture</c>. Esa divergencia
/// es preexistente y quedó como deuda: cambiarla desde acá podría alterar su
/// cálculo de pesos.
/// </para>
/// </remarks>
public static class ParseoNumerico
{
    /// <summary>Decimal opcional: vacío devuelve <c>null</c> y es válido.</summary>
    public static bool EsDecimalOpcional(string? texto, out decimal? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out var resultado))
            return false;

        valor = resultado;
        return true;
    }

    /// <summary>Entero opcional: vacío devuelve <c>null</c> y es válido.</summary>
    public static bool EsEnteroOpcional(string? texto, out int? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.CurrentCulture, out var resultado))
            return false;

        valor = resultado;
        return true;
    }

    /// <summary>
    /// ¿El texto es, como mucho, un decimal "en progreso" válido? No hace falta que ya
    /// esté completo — "12", "12" + separador solo, o el signo solo, cuentan como válidos
    /// a mitad de tecleo. Pensado para filtrar el tecleo/pegado ANTES de que la letra entre
    /// al campo (<see cref="EsDecimalOpcional"/> valida el valor ya completo, al salir del
    /// campo o guardar).
    /// </summary>
    public static bool PuedeSerDecimalEnProgreso(string texto)
    {
        if (texto.Length == 0) return true;

        string separador = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        string patron = $@"^-?\d*{Regex.Escape(separador)}?\d*$";
        return Regex.IsMatch(texto, patron);
    }
}
