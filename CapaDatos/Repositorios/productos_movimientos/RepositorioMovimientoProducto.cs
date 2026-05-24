using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioMovimientoProducto
    {
        /// <summary>Productos de un camión con sus indicadores en tiempo real (desde la vista)</summary>
        public static async Task<List<MovProductoResumen>> ObtenerResumenPorMovimientoAsync(int idMovimiento)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<MovProductoResumen>()
                    .Filter("id_movimiento", Supabase.Postgrest.Constants.Operator.Equals, idMovimiento)
                    .Get();
                return resultado?.Models ?? new List<MovProductoResumen>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener resumen de productos del movimiento {IdMovimiento}", idMovimiento);
                throw;
            }
        }

        /// <summary>Productos de un camión con datos del producto (para grilla de productos)</summary>
        public static async Task<List<MovimientoProducto>> ObtenerPorMovimientoAsync(int idMovimiento)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<MovimientoProducto>()
                    .Select("*, productos(*)")
                    .Filter("id_movimiento", Supabase.Postgrest.Constants.Operator.Equals, idMovimiento)
                    .Get();
                return resultado?.Models ?? new List<MovimientoProducto>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener productos del movimiento {IdMovimiento}", idMovimiento);
                throw;
            }
        }

        /// <summary>Agrega un producto al camión — retorna el registro con su ID generado</summary>
        public static async Task<MovimientoProducto?> CrearAsync(MovimientoProducto movProducto)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var response = await client
                    .From<MovimientoProducto>()
                    .Insert(movProducto);
                return response.Model;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al agregar producto al movimiento");
                throw;
            }
        }

        /// <summary>Cierra el pesaje de un producto específico dentro del camión</summary>
        public static async Task CerrarAsync(int idMovProducto)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                await client
                    .From<MovimientoProducto>()
                    .Where(mp => mp.idMovProducto == idMovProducto)
                    .Set(mp => mp.idEstado, EstadosPesaje.Cerrado)
                    .Update();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al cerrar movimiento-producto {IdMovProducto}", idMovProducto);
                throw;
            }
        }
    }
}
