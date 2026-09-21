using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using CapaDominio.Reglas;
using ServicioConexión.Conexion;

namespace CapaDatos.Auth;

/// <summary>
/// Cambio de contraseña propia del usuario autenticado, contra Supabase Auth (Gotrue).
/// </summary>
/// <remarks>
/// Misma convención que <see cref="AuthService"/> y <see cref="RecuperacionPasswordService"/>:
/// el detalle técnico va al log y al usuario le llega un mensaje corto, nunca lanza por
/// un fallo esperado.
/// </remarks>
public class CambioPropiaPasswordService : ICambioPropiaPasswordService
{
    public async Task<Result> CambiarAsync(
        string correo,
        string contrasenaActual,
        string nuevaPassword,
        CancellationToken ct = default)
    {
        // Reglas del dominio definidas contra Supabase Auth; la UI las evalúa para habilitar
        // el botón, y se revalidan acá en defensa en profundidad.
        if (!ReglasContrasena.CumpleTodasLasReglas(nuevaPassword ?? string.Empty))
            return Result.Fail("La nueva contraseña no cumple las reglas de seguridad.");

        try
        {
            var client = await ConexionSupabase.GetClientAsync();

            // 1) Prueba de dueño: re-autenticación con la contraseña actual, el mismo
            // método de ingreso del sistema (AuthService.LoginAsync). El session queda
            // sobre este mismo cliente, lo que deja preparado el paso 2.
            var sesion = await client.Auth.SignInWithPassword(correo, contrasenaActual);
            if (sesion?.User is null || string.IsNullOrWhiteSpace(sesion.User.Id))
                return Result.Fail("La contraseña actual no es correcta.");

            // 2) La nueva contraseña sobre la sesión real recién re-validada. Es la misma
            // llamada que hace la recuperación (Auth.Update); la diferencia es que acá NO
            // se cierra la sesión: es la sesión real, no una de recuperación.
            // Las reglas de dominio ya la validaron al completo.
            await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = nuevaPassword });

            return Result.Ok();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[CambioPropiaPasswordService] Error en CambiarAsync");
            return Result.Fail("No se pudo actualizar la contraseña. Inténtalo de nuevo.");
        }
    }
}
