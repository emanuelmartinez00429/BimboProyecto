using CapaAplicacion.Auth.Dtos;
using CapaAplicacion.Auth.Interfaces;
using CapaAplicacion.Common;
using CapaDatos.Repositorios.Usuario;
using ServicioConexión.Conexion;

namespace CapaDatos.Auth;

public class AuthService : IAuthService
{
    public async Task<Result<LoginResultDto>> LoginAsync(
        string email, string password, CancellationToken ct = default)
    {
        try
        {
            var client  = await ConexionSupabase.GetClientAsync();
            var session = await client.Auth.SignInWithPassword(email, password);

            if (session?.User == null)
                return Result<LoginResultDto>.Fail("Credenciales incorrectas.");

            var usuario = await RepositorioUsuario.ObtenerPorUuidAsync(session.User.Id!);
            if (usuario == null)
            {
                await client.Auth.SignOut();
                return Result<LoginResultDto>.Fail("Usuario no registrado en el sistema.");
            }

            // #7: Verificar que la cuenta esté habilitada (id_estado = 1)
            if (usuario.idEstado != 1)
            {
                await client.Auth.SignOut();
                return Result<LoginResultDto>.Fail("Tu cuenta está deshabilitada. Contacta al administrador.");
            }

            return Result<LoginResultDto>.Ok(new LoginResultDto
            {
                IdUsuario = usuario.idUsuario,
                Email     = email,
                IdRol     = usuario.idRol,
            });
        }
        catch (Exception ex)
        {
            // #3: Loguear internamente sin exponer detalles al usuario
            Serilog.Log.Error(ex, "[AuthService] Error en LoginAsync");
            return Result<LoginResultDto>.Fail("Error al iniciar sesión. Inténtalo de nuevo.");
        }
    }
}
