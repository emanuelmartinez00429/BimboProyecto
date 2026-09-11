using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using CapaDatos.Repositorios.Usuario;
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

            // Antes de mandar el correo, verificar que la cuenta no esté deshabilitada.
            // Función de solo lectura y anónima (verificar_cuenta_habilitada_por_correo):
            // no requiere prueba de dueño, a diferencia de AuthService.LoginAsync y
            // VerificarCodigoAsync, que solo pueden chequear id_estado DESPUÉS de que el
            // usuario demostró ser el dueño (contraseña u OTP). Devuelve true también si
            // el correo no existe, para no ampliar la enumeración de cuentas — por eso el
            // mensaje de abajo es deliberadamente genérico, no "cuenta deshabilitada": así
            // quien esté probando correos al azar no puede distinguir una cuenta
            // deshabilitada de un error técnico cualquiera.
            var habilitada = await client.Rpc(
                "verificar_cuenta_habilitada_por_correo",
                new Dictionary<string, object?> { ["p_correo"] = email });

            if (bool.TryParse(habilitada?.Content, out var esHabilitada) && !esHabilitada)
                return Result.Fail("No pudimos procesar tu solicitud. Si el problema continúa, contacta a soporte.");

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
            if (session?.User is null || string.IsNullOrWhiteSpace(session.User.Id))
                return Result.Fail("Código incorrecto o expirado. Inténtalo de nuevo.");

            // P-059: la cuenta tiene que existir en el sistema y estar habilitada
            // (id_estado = 1). Mismo chequeo y mismos mensajes que AuthService.LoginAsync.
            // Revelar el motivo aca no rompe la no-enumeracion de cuentas: a esta altura
            // el usuario ya demostro que es duenio del correo al validar el OTP.
            var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id);
            if (usuario is null)
            {
                await client.Auth.SignOut();
                return Result.Fail("Usuario no registrado en el sistema.");
            }
            if (usuario.idEstado != 1)
            {
                await client.Auth.SignOut();
                return Result.Fail("Tu cuenta está deshabilitada. Contacta al administrador.");
            }

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
