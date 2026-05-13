using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using Supabase.Gotrue.Exceptions;

namespace CapaDatos.Repositorios.Usuario
{
    public class RepositorioUsuario
    {
        public static async Task<Usuarios> ObtenerPorUuidAsync(string uuid)
        {
            try
            {

                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Usuarios>()
                    .Filter("uuid_usuario", Supabase.Postgrest.Constants.Operator.Equals, uuid)
                    .Get();
                return resultado?.Models?.FirstOrDefault();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener usuarios por uuid: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<usuarioVista>> obtenerUsuarios()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                                           .From<usuarioVista>()
                                           .Select("*, roles(*), empleados(*)")
                                           .Order("id_usuario", Supabase.Postgrest.Constants.Ordering.Ascending)
                                           .Get();
                return resultado?.Models ?? new List<usuarioVista>();
            }
            catch (Exception ex) 
            {
                Console.WriteLine($"Error al obtener usuarios: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<Roles>> obtenerRoles()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Roles>()
                    .Get();
                return resultado?.Models ?? new List<Roles>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al obtener roles: " + ex.Message);
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
                Console.WriteLine($"Error inesperado en Registro: {ex.Message}");
                throw;
            }
        }
    }
}
