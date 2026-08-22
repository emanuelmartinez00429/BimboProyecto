namespace CapaDominio.Reglas;

/// <summary>
/// Reglas del formulario de inicio de sesion y del flujo de recuperacion
/// de contrasena (OTP).
/// </summary>
/// <remarks>
/// <para>
/// Viven en el dominio porque son politicas del negocio, no decisiones de
/// pantalla: que el OTP tenga 8 digitos numericos es un contrato con el
/// proveedor de autenticacion (Supabase), no un capricho de la UI.
/// </para>
/// <para>
/// Sin dependencias externas: solo BCL (System.Linq, System.Collections.Generic).
/// </para>
/// </remarks>
public static class ReglasLogin
{
    // ── Credenciales de acceso ────────────────────────────────────────────────

    /// <summary>
    /// Ambos campos deben tener contenido para habilitar el boton Ingresar.
    /// </summary>
    /// <param name="email">Texto del campo de correo (sin sufijo de dominio).</param>
    /// <param name="password">Contrasena en texto plano (no se valida complejidad aqui).</param>
    public static bool CredencialesCompletas(string email, string password)
        => ReglasFormato.TieneContenido(email) && password.Length > 0;

    // ── OTP de recuperacion ───────────────────────────────────────────────────

    /// <summary>Longitud exacta del codigo OTP que emite Supabase Auth.</summary>
    public const int LongitudOtp = 8;

    /// <summary>
    /// Un OTP es valido si tiene exactamente <see cref="LongitudOtp"/> caracteres
    /// y todos son digitos numericos. Se usa al pegar desde el portapapeles.
    /// </summary>
    public static bool OtpValido(string otp)
        => otp.Length == LongitudOtp && otp.All(char.IsDigit);

    /// <summary>
    /// Todas las casillas del input OTP tienen exactamente un caracter.
    /// Se usa para habilitar el boton "Verificar codigo".
    /// </summary>
    public static bool OtpCompleto(IEnumerable<string> casillas)
        => casillas.All(c => c.Length == 1);

    /// <summary>
    /// El caracter es un digito numerico. Se usa para bloquear la entrada
    /// de letras o simbolos en las casillas del OTP.
    /// </summary>
    public static bool EsDigitoOtp(char c) => char.IsDigit(c);
}
