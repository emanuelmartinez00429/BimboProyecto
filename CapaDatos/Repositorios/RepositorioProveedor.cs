using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios
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
                Console.WriteLine($"Error al obtener proveedores activos: {ex.Message}");
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
                Console.WriteLine($"Error al obtener proveedores: {ex.Message}");
                throw;
            }
        }
    }
}
