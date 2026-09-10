using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using ServicioConexión.Conexion;

namespace CapaDatos.Auth;

/// <summary>
/// Recuperación de contraseña contra Supabase Auth (Gotrue).
/// </summary>
/// <remarks>
/// Misma convención que <see cref="AuthService"/>: el detalle técnico va al log y al
/// usuario le llega un mensaje corto. Nunca lanza por un fallo esperado.
/// </remarks>
public class RecuperacionPasswordService : IRecuperacionPasswordService
{
    public async Task<Result> EnviarCodigoAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var client = await ConexionSupabase.GetClientAsync();
            await client.Auth.ResetPasswordForEmail(email);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            // A propósito NO se distingue "el correo no existe" de los demás fallos: el
            // proveedor tampoco lo hace, y hacerlo permitiría enumerar cuentas válidas.
            Serilog.Log.Error(ex, "[RecuperacionPasswordService] Error en EnviarCodigoAsync");
            return Result.Fail("No se pudo enviar el código. Verifica tu conexión e inténtalo de nuevo.");
        }
    }

    public async Task<Result> VerificarCodigoAsync(string email, string codigo, CancellationToken ct = default)
    {
        try
        {
            var client  = await ConexionSupabase.GetClientAsync();
            var session = await client.Auth.VerifyOTP(
                email, codigo, Supabase.Gotrue.Constants.EmailOtpType.Recovery);

            // Gotrue devuelve una sesión sin usuario cuando el código no valida, en vez
            // de lanzar. Sin este chequeo el flujo seguiría con una sesión vacía.
            if (session?.User is null)
                return Result.Fail("Código incorrecto o expirado. Inténtalo de nuevo.");

            return Result.Ok();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[RecuperacionPasswordService] Error en VerificarCodigoAsync");
            return Result.Fail("Código incorrecto o expirado. Inténtalo de nuevo.");
        }
    }

    public async Task<Result> CambiarPasswordAsync(string nuevaPassword, CancellationToken ct = default)
    {
        try
        {
            var client = await ConexionSupabase.GetClientAsync();

            // Opera sobre la sesión de recuperación que dejó abierta VerificarCodigoAsync.
            await client.Auth.Update(new Supabase.Gotrue.UserAttributes { Password = nuevaPassword });

            // Cerrar la sesión es parte del cambio, no limpieza opcional: obliga a entrar
            // de nuevo con la contraseña nueva y no deja viva la sesión de recuperación.
            await client.Auth.SignOut();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[RecuperacionPasswordService] Error en CambiarPasswordAsync");
            return Result.Fail("No se pudo actualizar la contraseña. Inténtalo de nuevo.");
        }
    }
}
