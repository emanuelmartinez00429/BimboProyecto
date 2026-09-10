using CapaAplicacion.Common;

namespace CapaAplicacion.Auth.Interfaces;

/// <summary>
/// Recuperación de contraseña por código de un solo uso (OTP) enviado al correo.
/// </summary>
/// <remarks>
/// <para>
/// Existe para sacar de la capa de UI las llamadas directas al proveedor de
/// autenticación. Los tres paneles de recuperación hacían
/// <c>ConexionSupabase.GetClientAsync()</c> y le hablaban a <c>client.Auth</c> desde
/// el code-behind, salteándose esta capa y la de datos — y además a través del
/// proyecto huérfano <c>ServicioConexión</c> (ver <c>P-056</c>). Ver <c>P-058</c>.
/// </para>
/// <para>
/// Los tres métodos devuelven <see cref="Result"/> y <b>nunca lanzan</b> por un fallo
/// esperado: la implementación traduce las excepciones del SDK a un mensaje apto para
/// mostrarle al usuario y deja el detalle técnico en el log. Es la misma convención
/// que <see cref="IAuthService"/>.
/// </para>
/// <para>
/// <b>El flujo es una secuencia con estado del lado del proveedor</b>, no tres
/// operaciones sueltas: <see cref="VerificarCodigoAsync"/> deja una sesión de
/// recuperación abierta, y <see cref="CambiarPasswordAsync"/> opera sobre <b>esa</b>
/// sesión — por eso no recibe el correo ni el código. Llamarlo sin haber verificado
/// antes falla.
/// </para>
/// </remarks>
public interface IRecuperacionPasswordService
{
    /// <summary>
    /// Envía al correo indicado un código de recuperación.
    /// </summary>
    /// <remarks>
    /// Devuelve <c>Ok</c> aunque el correo no exista: informar lo contrario permitiría
    /// enumerar cuentas válidas. El usuario ve siempre el mismo mensaje.
    /// </remarks>
    Task<Result> EnviarCodigoAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Valida el código contra el correo y, si es correcto, abre la sesión de
    /// recuperación que habilita el cambio de contraseña.
    /// </summary>
    Task<Result> VerificarCodigoAsync(string email, string codigo, CancellationToken ct = default);

    /// <summary>
    /// Cambia la contraseña del usuario de la sesión de recuperación abierta por
    /// <see cref="VerificarCodigoAsync"/> y cierra esa sesión, para que el cambio
    /// obligue a entrar de nuevo con la contraseña nueva.
    /// </summary>
    Task<Result> CambiarPasswordAsync(string nuevaPassword, CancellationToken ct = default);
}
