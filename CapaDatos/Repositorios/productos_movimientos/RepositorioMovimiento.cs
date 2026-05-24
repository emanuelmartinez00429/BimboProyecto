using CapaDatos.Modelados.Pesajes;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioMovimiento
    {
        /// <summary>Camiones actualmente abiertos (para grilla principal)</summary>
        public static async Task<List<Movimiento>> ObtenerAbiertosAsync()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsObtener = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client
                    .From<Movimiento>()
                    .Select("*, proveedores(*)")
                    .Filter("id_estado", Supabase.Postgrest.Constants.Operator.Equals, EstadosPesaje.Abierto)
                    .Order("fecha_asignacion", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get(ctsObtener.Token);
                return resultado?.Models ?? new List<Movimiento>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener movimientos abiertos");
                throw;
            }
        }

        /// <summary>Filtrado por fecha, proveedor y estado para vista histórica</summary>
        public static async Task<List<Movimiento>> ObtenerPorFiltrosAsync(
            DateOnly? desde, DateOnly? hasta, int? idProveedor, int? idEstado)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var query = client
                    .From<Movimiento>()
                    .Select("*, proveedores(*)");

                if (desde.HasValue)
                    query = query.Filter("fecha_asignacion", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, desde.Value.ToString("yyyy-MM-dd"));
                if (hasta.HasValue)
                    query = query.Filter("fecha_asignacion", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, hasta.Value.ToString("yyyy-MM-dd"));
                if (idProveedor.HasValue)
                    query = query.Filter("id_proveedor", Supabase.Postgrest.Constants.Operator.Equals, idProveedor.Value);
                if (idEstado.HasValue)
                    query = query.Filter("id_estado", Supabase.Postgrest.Constants.Operator.Equals, idEstado.Value);

                using var ctsFiltrar = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await query
                    .Order("fecha_asignacion", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get(ctsFiltrar.Token);
                return resultado?.Models ?? new List<Movimiento>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al filtrar movimientos");
                throw;
            }
        }

        /// <summary>Registra un nuevo camión — retorna el movimiento con su ID generado</summary>
        public static async Task<Movimiento?> CrearAsync(Movimiento movimiento)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsCrear = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var response = await client
                    .From<Movimiento>()
                    .Insert(movimiento, cancellationToken: ctsCrear.Token);
                return response.Model;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al crear movimiento");
                throw;
            }
        }

        /// <summary>Cierra el movimiento — todos sus productos deben estar cerrados antes</summary>
        public static async Task CerrarAsync(int idMovimiento)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsCerrar = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                await client
                    .From<Movimiento>()
                    .Where(m => m.idMovimiento == idMovimiento)
                    .Set(m => m.idEstado, EstadosPesaje.Cerrado)
                    .Update(null, ctsCerrar.Token);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al cerrar movimiento {IdMovimiento}", idMovimiento);
                throw;
            }
        }

        /// <summary>Anula el movimiento completo</summary>
        public static async Task AnularAsync(int idMovimiento)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsAnular = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                await client
                    .From<Movimiento>()
                    .Where(m => m.idMovimiento == idMovimiento)
                    .Set(m => m.idEstado, EstadosPesaje.Anulado)
                    .Update(null, ctsAnular.Token);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al anular movimiento {IdMovimiento}", idMovimiento);
                throw;
            }
        }
    }
}
