using System.Globalization;
using System.Text.RegularExpressions;

namespace CapaUI.Core.Validacion;

/// <summary>
/// Reglas de validación puras: responden si un valor es válido y nada más.
/// </summary>
/// <remarks>
/// <para>
/// <b>Acá no entra nada de UI.</b> Sin <c>MessageBox</c>, sin <c>Focus()</c>, sin
/// <c>TextBox</c>. Esa separación es deliberada y es lo que hace la capa
/// reutilizable: <see cref="ValidadorFormulario"/> la usa desde los modales de
/// code-behind, pero <c>ConfiguracionEmpresaViewModel</c> —que es MVVM puro y no
/// tiene controles que pasarle a un validador— consume estas mismas reglas
/// directamente. Si la regla mostrara el mensaje por su cuenta, serviría en un
/// solo lugar.
/// </para>
/// <para>
/// Las reglas de formato (correo, RTN, teléfono) tratan el vacío como
/// <b>válido</b>: "esto es opcional, pero si lo llenás tiene que estar bien". La
/// obligatoriedad es una regla aparte, y así se pueden combinar sin que un campo
/// opcional con formato quede implícitamente obligatorio.
/// </para>
/// </remarks>
public static class ReglasCampo
{
    /// <summary>
    /// Correo con forma <c>algo@algo.algo</c>. Es la misma expresión que ya usaba
    /// el panel de recuperación de contraseña, movida acá porque era la única de
    /// todo el proyecto y estaba encerrada en un solo flujo.
    ///
    /// Deliberadamente permisiva: validar correos "de verdad" contra el RFC es un
    /// pozo sin fondo y termina rechazando direcciones legítimas. Lo que ataja es
    /// el error de tipeo real (falta la arroba, falta el punto, quedó un espacio).
    /// </summary>
    private static readonly Regex RegexCorreo =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>
    /// RTN hondureño: 14 dígitos. Se ignoran guiones y espacios, así que tanto
    /// "08011985123456" como "0801-1985-123456" pasan.
    /// </summary>
    private static readonly Regex RegexRtn = new(@"^\d{14}$", RegexOptions.Compiled);

    /// <summary>
    /// Teléfono: 8 dígitos (formato hondureño) o hasta 15 con código de país, que
    /// es el máximo del estándar E.164. Se ignoran separadores y un "+" inicial.
    /// </summary>
    private static readonly Regex RegexTelefono = new(@"^\+?\d{8,15}$", RegexOptions.Compiled);

    /// <summary>¿Tiene contenido más allá de espacios en blanco?</summary>
    public static bool TieneContenido(string? texto) => !string.IsNullOrWhiteSpace(texto);

    /// <summary>Vacío = válido. Ver remarks de la clase.</summary>
    public static bool EsCorreo(string? texto) =>
        !TieneContenido(texto) || RegexCorreo.IsMatch(texto!.Trim());

    /// <summary>Vacío = válido. Tolera guiones y espacios como separadores.</summary>
    public static bool EsRtn(string? texto) =>
        !TieneContenido(texto) || RegexRtn.IsMatch(SoloDigitos(texto!));

    /// <summary>Vacío = válido. Tolera guiones, espacios, paréntesis y "+".</summary>
    public static bool EsTelefono(string? texto)
    {
        if (!TieneContenido(texto)) return true;

        var limpio = texto!.Trim();
        var signo  = limpio.StartsWith('+') ? "+" : string.Empty;
        return RegexTelefono.IsMatch(signo + SoloDigitos(limpio));
    }

    public static bool NoExcedeLargo(string? texto, int largoMaximo) =>
        (texto?.Trim().Length ?? 0) <= largoMaximo;

    public static bool TieneLargoMinimo(string? texto, int largoMinimo) =>
        (texto?.Length ?? 0) >= largoMinimo;

    /// <summary>
    /// Decimal opcional: vacío devuelve <c>null</c> y es válido.
    /// </summary>
    /// <remarks>
    /// Usa <see cref="CultureInfo.CurrentCulture"/> a propósito — es un número que
    /// tipeó una persona, así que el separador decimal tiene que ser el de su
    /// configuración regional, no el invariante.
    ///
    /// ⚠️ Los modales de Pesaje parsean con <c>InvariantCulture</c>. Esa divergencia
    /// es preexistente y queda como deuda: cambiarla de refilón desde acá podría
    /// romper su cálculo de pesos.
    /// </remarks>
    public static bool EsDecimalOpcional(string? texto, out decimal? valor)
    {
        valor = null;
        if (!TieneContenido(texto)) return true;

        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out var resultado))
            return false;

        valor = resultado;
        return true;
    }

    public static bool EsEnteroOpcional(string? texto, out int? valor)
    {
        valor = null;
        if (!TieneContenido(texto)) return true;

        if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.CurrentCulture, out var resultado))
            return false;

        valor = resultado;
        return true;
    }

    private static string SoloDigitos(string texto)
    {
        Span<char> destino = texto.Length <= 64 ? stackalloc char[texto.Length] : new char[texto.Length];
        int usados = 0;

        foreach (var caracter in texto)
            if (char.IsDigit(caracter)) destino[usados++] = caracter;

        return new string(destino[..usados]);
    }
}
