using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioTara
    {
        public static async Task<List<Tara>> ObtenerTodosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Tara>()
                    .Get();
                return resultado?.Models ?? new List<Tara>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener taras");
                throw;
            }
        }
    }
}
