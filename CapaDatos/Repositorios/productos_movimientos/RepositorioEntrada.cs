using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioEntrada
    {
        /// <summary>
        /// Pesajes de un producto específico dentro de un movimiento.
        /// El trigger de BD calcula peso_neto, peso_tara_total y peso_tara_individual automáticamente.
        /// </summary>
        public static async Task<List<EntradaProducto>> ObtenerPorMovProductoAsync(int idMovProducto)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                    .From<EntradaProducto>()
                    .Filter("id_mov_producto", Supabase.Postgrest.Constants.Operator.Equals, idMovProducto)
                    .Order("fecha_entrada", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Order("hora_entrada", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                return resultado?.Models ?? new List<EntradaProducto>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener entradas del mov-producto {IdMovProducto}", idMovProducto);
                throw;
            }
        }

        /// <summary>
        /// Guarda un pesaje. Solo enviar: id_mov_producto, id_producto, peso_bruto, peso_tara_extra,
        /// numero_bultos_recibido, id_usuario, id_estado, id_tara, id_tarima, observaciones.
        /// La BD calcula y persiste peso_neto, peso_tara_total y peso_tara_individual via trigger.
        /// </summary>
        public static async Task<EntradaProducto?> CrearAsync(EntradaProducto entrada)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var response = await client
                    .From<EntradaProducto>()
                    .Insert(entrada);
                return response.Model;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al guardar entrada de pesaje");
                throw;
            }
        }

        /// <summary>Elimina un pesaje erróneo — solo si el movimiento-producto sigue Abierto</summary>
        public static async Task EliminarAsync(int idPesaje)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                await client
                    .From<EntradaProducto>()
                    .Where(e => e.idPesaje == idPesaje)
                    .Delete();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al eliminar entrada {IdPesaje}", idPesaje);
                throw;
            }
        }
    }
}
