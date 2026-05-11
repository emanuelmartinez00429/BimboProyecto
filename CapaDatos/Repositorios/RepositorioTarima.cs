using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios
{
    public class RepositorioTarima
    {
        public static async Task<List<Tarima>> ObtenerTodosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Tarima>()
                    .Get();
                return resultado?.Models ?? new List<Tarima>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener tarimas: {ex.Message}");
                throw;
            }
        }
    }
}
