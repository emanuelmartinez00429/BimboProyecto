using System.Text.RegularExpressions;

namespace CapaDominio.Reglas;

/// <summary>
/// Formatos que define el negocio. Responden si un valor es válido y nada más.
/// </summary>
/// <remarks>
/// <para>
/// Viven en el dominio porque son verdaderas con o sin interfaz: que un RTN
/// hondureño tenga 14 dígitos es ley tributaria, no una decisión de pantalla.
/// Mismo criterio que <see cref="PesoCalculator"/>, que ya estaba acá — el
/// formulario las usa para avisar temprano, pero nada impide que un repositorio
/// las use antes de viajar a la red.
/// </para>
/// <para>
/// <b>El vacío se considera válido</b> en las reglas de formato: significan "es
/// opcional, pero si lo llenás tiene que estar bien". La obligatoriedad se
/// declara aparte (<see cref="ReglaCampo.Obligatorio"/>), y así un campo
/// opcional con formato no queda obligatorio sin querer.
/// </para>
/// </remarks>
public static class ReglasFormato
{
    /// <summary>
    /// Correo con forma <c>algo@algo.algo</c>.
    /// </summary>
    /// <remarks>
    /// Deliberadamente permisiva: validar correos contra el RFC al pie de la letra
    /// es un pozo sin fondo y termina rechazando direcciones legítimas. Lo que
    /// ataja es el error de tipeo real — falta la arroba, falta el punto, quedó
    /// un espacio.
    /// </remarks>
    private static readonly Regex RegexCorreo =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    /// <summary>RTN hondureño: 14 dígitos ASCII; admite solo espacios y guiones como separadores.</summary>
    private static readonly Regex RegexRtnCaracteresPermitidos = new(@"^[0-9 -]+$", RegexOptions.Compiled);

    /// <summary>
    /// Teléfono: 8 dígitos (formato hondureño) o hasta 15 con código de país,
    /// que es el máximo del estándar E.164.
    /// </summary>
    private static readonly Regex RegexTelefonoCaracteresPermitidos = new(@"^\+?[0-9 -]+$", RegexOptions.Compiled);

    public static bool TieneContenido(string? texto) => !string.IsNullOrWhiteSpace(texto);

    public static bool EsCorreo(string? texto) =>
        !TieneContenido(texto) || RegexCorreo.IsMatch(texto!.Trim());

    public static bool EsRtn(string? texto)
    {
        if (!TieneContenido(texto)) return true;

        var limpio = texto!.Trim();
        return RegexRtnCaracteresPermitidos.IsMatch(limpio)
            && ContarDigitosAscii(limpio) == 14;
    }

    public static bool EsTelefono(string? texto)
    {
        if (!TieneContenido(texto)) return true;

        var limpio = texto!.Trim();
        return RegexTelefonoCaracteresPermitidos.IsMatch(limpio)
            && ContarDigitosAscii(limpio) is >= 8 and <= 15;
    }

    public static bool NoExcedeLargo(string? texto, int largoMaximo) =>
        (texto?.Trim().Length ?? 0) <= largoMaximo;

    public static bool TieneLargoMinimo(string? texto, int largoMinimo) =>
        (texto?.Length ?? 0) >= largoMinimo;

    /// <summary>
    /// Rango numérico exclusivo por abajo (mayor a <paramref name="min"/>) e inclusivo
    /// por arriba (hasta <paramref name="max"/>). El parseo del texto tipeado es cosa de
    /// la capa de presentación (mismo criterio que <see cref="FormatoCampo.Decimal"/>) —
    /// acá solo se compara el valor ya parseado. Vacío (<c>null</c>) es válido, igual que
    /// el resto de las reglas de formato.
    /// </summary>
    public static bool EstaEnRango(decimal? valor, decimal min, decimal max) =>
        valor is null || (valor > min && valor <= max);

    /// <summary>
    /// Normaliza un color hexadecimal a la forma canónica <c>#RRGGBB</c> en mayúsculas.
    /// Acepta 6 caracteres sin <c>#</c> (se lo antepone) o 7 con <c>#</c>; no valida que
    /// los caracteres sean hexadecimales — eso es responsabilidad de quien valida el
    /// formato antes de guardar (ver <c>EmpresaThemeService.EsColorValido</c>). Vacío
    /// normaliza a cadena vacía.
    /// </summary>
    public static string NormalizarColorHex(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        var texto = valor.Trim();
        if (texto.Length == 6 && !texto.StartsWith('#'))
            texto = "#" + texto;
        return texto.ToUpperInvariant();
    }

    /// <summary>
    /// Compara dos valores de texto recibidos por WAL de Realtime contra el valor
    /// actual en memoria, ignorando mayúsculas/minúsculas y espacios al borde.
    /// </summary>
    public static bool ValoresRealtimeCoinciden(string? valorRecibido, string? valorActual) =>
        string.Equals((valorRecibido ?? string.Empty).Trim(), (valorActual ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Igual que <see cref="ValoresRealtimeCoinciden"/> pero para colores: normaliza
    /// ambos lados con <see cref="NormalizarColorHex"/> antes de comparar, así que
    /// "1e3a8a" (sin '#', minúscula) y "#1E3A8A" se consideran el mismo valor.
    /// </summary>
    public static bool ColoresRealtimeCoinciden(string? valorRecibido, string? valorActual) =>
        string.Equals(NormalizarColorHex(valorRecibido), NormalizarColorHex(valorActual),
            StringComparison.OrdinalIgnoreCase);

    private static int ContarDigitosAscii(string texto) =>
        texto.Count(caracter => caracter is >= '0' and <= '9');
}
