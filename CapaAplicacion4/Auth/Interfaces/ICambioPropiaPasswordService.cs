using CapaAplicacion.Common;

namespace CapaAplicacion.Auth.Interfaces;

/// <summary>
/// Cambio de contraseña por el propio usuario autenticado desde «Mi Usuario».
/// </summary>
/// <remarks>
/// <para>
/// Reutiliza el <b>mismo mecanismo</b> que <see cref="IRecuperacionPasswordService"/>.
/// CambiarPasswordAsync, con una diferencia por diseño: la recuperación opera sobre una
/// <i>sesión de recuperación</i> que muere al terminar (el usuario tiene que reloguear),
/// acá la sesión que se usa es la real del usuario y <b>no</b> se cierra — el cambio es
/// transparente para el resto de la app.
/// </para>
/// <para>
/// La prueba de dueño es la re-autenticación: <see cref="LoginAsync"/> del mismo
///AuthService valida la contraseña actual antes de tocar nada. Misma convención de
/// <c>Result</c> sin lanzar que el resto de los servicios de auth.
/// </para>
/// </remarks>
public interface ICambioPropiaPasswordService
{
    /// <summary>
    /// Re-autentica al usuario con su contraseña actual y, si es correcta, fija la nueva.
    /// </summary>
    Task<Result> CambiarAsync(
        string correo,
        string contrasenaActual,
        string nuevaPassword,
        CancellationToken ct = default);
}
