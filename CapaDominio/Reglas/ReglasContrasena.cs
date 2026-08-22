namespace CapaDominio.Reglas;

/// <summary>
/// Reglas de complejidad que el negocio exige para una contrasena de usuario.
/// </summary>
/// <remarks>
/// <para>
/// Vive en el dominio porque las reglas son verdaderas con o sin interfaz:
/// que una contrasena necesite al menos 8 caracteres y un simbolo no es una
/// decision de pantalla, es una politica de seguridad del negocio.
/// </para>
/// <para>
/// La UI y cualquier otro consumidor llaman a estas funciones en lugar de
/// reimplementar la logica localmente — fuente unica de verdad.
/// </para>
/// <para>
/// Sin dependencias externas: solo BCL (System.Linq).
/// </para>
/// </remarks>
public static class ReglasContrasena
{
    // ── Criterios individuales ────────────────────────────────────────────────
    // Cada uno responde a una sola pregunta de si/no. La UI los usa para
    // pintar los indicadores R1–R4; Validate() los combina para el boton.

    /// <summary>Al menos 8 caracteres (requisito base obligatorio).</summary>
    public static bool TieneLargoMinimo(string pwd) => pwd.Length >= 8;

    /// <summary>Al menos una letra mayuscula.</summary>
    public static bool TieneMayuscula(string pwd) => pwd.Any(char.IsUpper);

    /// <summary>Al menos un digito numerico.</summary>
    public static bool TieneNumero(string pwd) => pwd.Any(char.IsDigit);

    /// <summary>Al menos un simbolo (caracter que no es letra ni digito).</summary>
    public static bool TieneSimbolo(string pwd) => pwd.Any(c => !char.IsLetterOrDigit(c));

    // ── Validacion completa ───────────────────────────────────────────────────

    /// <summary>
    /// La contrasena cumple TODAS las reglas del negocio: largo, mayuscula,
    /// numero y simbolo. Es la condicion que habilita el boton "Actualizar".
    /// </summary>
    public static bool CumpleTodasLasReglas(string pwd)
        => TieneLargoMinimo(pwd)
        && TieneMayuscula(pwd)
        && TieneNumero(pwd)
        && TieneSimbolo(pwd);

    // ── Score de fortaleza ────────────────────────────────────────────────────

    /// <summary>
    /// Calcula un score de fortaleza de 0 a 5 para uso del medidor visual.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La longitud minima es prerequisito: sin ella el score es 0, aunque
    /// se cumplan otros criterios. Esto garantiza que el medidor nunca muestre
    /// "Aceptable" cuando el boton sigue deshabilitado por falta de largo.
    /// </para>
    /// <list type="bullet">
    ///   <item>0 — vacio o largo insuficiente</item>
    ///   <item>1 — largo minimo cumplido</item>
    ///   <item>2 — + mayuscula</item>
    ///   <item>3 — + numero</item>
    ///   <item>4 — + simbolo (contrasena valida)</item>
    ///   <item>5 — + largo >= 12 (bonus)</item>
    /// </list>
    /// </remarks>
    public static int CalcularScore(string pwd)
    {
        if (string.IsNullOrEmpty(pwd)) return 0;

        int score = 0;
        if (TieneLargoMinimo(pwd))
        {
            score++;
            if (TieneMayuscula(pwd)) score++;
            if (TieneNumero(pwd))    score++;
            if (TieneSimbolo(pwd))   score++;
            if (pwd.Length >= 12)    score++;
        }
        return Math.Clamp(score, 0, 5);
    }
}
