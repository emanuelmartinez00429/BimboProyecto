using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioProveedor
    {
        public static async Task<List<Proveedores>> ObtenerActivosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Proveedores>()
                    .Filter("id_estado", Supabase.Postgrest.Constants.Operator.Equals, EstadosPesaje.Activo)
                    .Order("nombre_proveedor", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return resultado?.Models ?? new List<Proveedores>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener proveedores activos");
                throw;
            }
        }

        public static async Task<List<Proveedores>> ObtenerTodosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<Proveedores>()
                    .Order("nombre_proveedor", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return resultado?.Models ?? new List<Proveedores>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener proveedores");
                throw;
            }
        }
    }
}
