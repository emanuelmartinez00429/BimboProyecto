using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;

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
        /*
        public static async Task<Usuarios> registrarUsuario(string email, string pass)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client.Auth.SignUp(email,pass);
                    

                //return resultado?.Models ?? new List<Usuarios>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al registrar usuario: {ex.Message}");
                throw;
            }
        }*/
    }
}
