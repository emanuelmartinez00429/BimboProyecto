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
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<Tara>()
                    .Get(cts.Token);
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
