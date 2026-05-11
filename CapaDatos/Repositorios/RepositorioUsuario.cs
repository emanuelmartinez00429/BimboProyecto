using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios
{
    public class RepositorioUsuario
    {
        public static async Task<Usuarios?> ObtenerPorUuidAsync(string uuid)
        {
            var client = await ConexionSupabase.GetClientAsync();
            var resultado = await client
                .From<Usuarios>()
                .Filter("uuid_usuario", Supabase.Postgrest.Constants.Operator.Equals, uuid)
                .Get();
            return resultado?.Models?.FirstOrDefault();
        }
    }
}
