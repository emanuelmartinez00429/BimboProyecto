using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Supabase.Gotrue.Exceptions;

namespace CapaDatos.Repositorios.Usuario
{
    public class RepositorioUsuario
    {
        public static async Task<Usuarios?> ObtenerPorUuidAsync(string uuid)
        {
            try
            {

                var client = await ConexionSupabase.GetClientAsync();
                using var ctsPorUuid = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<Usuarios>()
                    .Filter("uuid_usuario", Supabase.Postgrest.Constants.Operator.Equals, uuid)
                    .Get(ctsPorUuid.Token);
                return resultado?.Models?.FirstOrDefault();

            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener usuarios por uuid");
                throw;
            }
        }

        public static async Task<usuarioVista?> ObtenerPorIdAsync(int idUsuario)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsPorId = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<usuarioVista>()
                    .Select("*, roles(*), empleados(*)")
                    .Filter("id_usuario", Supabase.Postgrest.Constants.Operator.Equals, idUsuario.ToString())
                    .Get(ctsPorId.Token);
                return resultado?.Models?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener usuario por id");
                throw;
            }
        }

        public static async Task<List<usuarioVista>> obtenerUsuarios()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsObtener = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                                           .From<usuarioVista>()
                                           .Select("*, roles(*), empleados(*)")
                                           .Order("id_usuario", Supabase.Postgrest.Constants.Ordering.Ascending)
                                           .Get(ctsObtener.Token);
                return resultado?.Models ?? new List<usuarioVista>();
            }
            catch (Exception ex) 
            {
                Serilog.Log.Error(ex, "Error al obtener usuarios");
                throw;
            }
        }

        public static async Task<List<Roles>> obtenerRoles()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsRoles = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<Roles>()
                    .Get(ctsRoles.Token);
                return resultado?.Models ?? new List<Roles>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener roles");
                throw;
            }
        }
        
        public static async Task registrarUsuario(string email, string pass)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                await client.Auth.SignUp(email, pass);
                //return session;
            }
            catch (GotrueException ex)
            {

                throw new Exception($"Error de registro: {ex.Message}", ex);
            }
            catch (System.Net.WebException ex)
            {
                throw new Exception("Error de red al intentar registrarse: " + ex.Message, ex);
            }
            catch (TimeoutException ex)
            {
                throw new Exception("El servidor de registro tardó demasiado en responder.", ex);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error inesperado en Registro");
                throw;
            }
        }
    }
}
