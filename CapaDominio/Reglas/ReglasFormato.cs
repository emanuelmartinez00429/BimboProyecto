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

    /// <summary>RTN hondureño: 14 dígitos. Se ignoran guiones y espacios.</summary>
    private static readonly Regex RegexRtn = new(@"^\d{14}$", RegexOptions.Compiled);

    /// <summary>
    /// Teléfono: 8 dígitos (formato hondureño) o hasta 15 con código de país,
    /// que es el máximo del estándar E.164.
    /// </summary>
    private static readonly Regex RegexTelefono = new(@"^\+?\d{8,15}$", RegexOptions.Compiled);

    public static bool TieneContenido(string? texto) => !string.IsNullOrWhiteSpace(texto);

    public static bool EsCorreo(string? texto) =>
        !TieneContenido(texto) || RegexCorreo.IsMatch(texto!.Trim());

    public static bool EsRtn(string? texto) =>
        !TieneContenido(texto) || RegexRtn.IsMatch(SoloDigitos(texto!));

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

    private static string SoloDigitos(string texto)
    {
        Span<char> destino = texto.Length <= 64 ? stackalloc char[texto.Length] : new char[texto.Length];
        int usados = 0;

        foreach (var caracter in texto)
            if (char.IsDigit(caracter)) destino[usados++] = caracter;

        return new string(destino[..usados]);
    }
}
