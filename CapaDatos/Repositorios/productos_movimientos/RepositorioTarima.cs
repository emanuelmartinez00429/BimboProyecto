using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioTarima
    {
        public static async Task<List<Tarima>> ObtenerTodosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<Tarima>()
                    .Get(cts.Token);
                return resultado?.Models ?? new List<Tarima>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener tarimas");
                throw;
            }
        }
    }
}
