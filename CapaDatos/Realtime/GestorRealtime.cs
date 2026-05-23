using ServicioConexión.Conexion;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;
using CapaDatos.Modelados.Productos;
using CapaDatos.Modelados.Usuarios;
using CapaDatos.Modelados.Pesajes;


namespace CapaDominio
{
    public static class GestorRealtime
    {
        private static Supabase.Client? _client;
        private static RealtimeChannel? _productosChannel;
        private static RealtimeChannel? _categoriasChannel;
        private static RealtimeChannel? _empleadosChannel;
        private static RealtimeChannel? _usuariosChannel;
        private static RealtimeChannel? _movimientosChannel;
        private static RealtimeChannel? _movProductosChannel;

        private static bool _iniciado = false;
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public static event Action<PostgresChangesResponse>? OnProductosChanged;
        public static event Action<PostgresChangesResponse>? OnCategoriasChanged;
        public static event Action<PostgresChangesResponse>? OnEmpleadosChanged;
        public static event Action<PostgresChangesResponse>? OnUsuariosChanged;
        public static event Action<PostgresChangesResponse>? OnMovimientosChanged;
        public static event Action<PostgresChangesResponse>? OnMovProductosChanged;

        public static bool EstaIniciado => _iniciado;

        public static async Task IniciarAsync()
        {
            if (_iniciado) return;

            await _lock.WaitAsync();
            try
            {
                if (_iniciado) return;

                _client = await ConexionSupabase.GetClientAsync();

                if (_client == null)
                    throw new Exception("No se pudo crear el cliente de Supabase.");

                _productosChannel = await _client.From<Productos>().On(ListenType.All, (sender, change) =>
                {
                    try { OnProductosChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] productos: {ex.Message}"); }
                });

                _categoriasChannel = await _client.From<Categoria>().On(ListenType.All, (sender, change) =>
                {
                    try { OnCategoriasChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] categorias: {ex.Message}"); }
                });

                _empleadosChannel = await _client.From<Empleados>().On(ListenType.All, (sender, change) =>
                {
                    try { OnEmpleadosChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] empleados: {ex.Message}"); }
                });

                _usuariosChannel = await _client.From<Usuarios>().On(ListenType.All, (sender, change) =>
                {
                    try { OnUsuariosChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] usuarios: {ex.Message}"); }
                });

                _movimientosChannel = await _client.From<Movimiento>().On(ListenType.All, (sender, change) =>
                {
                    try { OnMovimientosChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] movimientos: {ex.Message}"); }
                });

                _movProductosChannel = await _client.From<MovimientoProducto>().On(ListenType.All, (sender, change) =>
                {
                    try { OnMovProductosChanged?.Invoke(change); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Realtime] movimiento_productos: {ex.Message}"); }
                });

                _iniciado = true;

                System.Diagnostics.Debug.WriteLine("GestorRealtime iniciado correctamente.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al iniciar GestorRealtime: {ex.Message}");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public static async Task DetenerAsync()
        {
            if (!_iniciado) return;

            try
            {
                if (_productosChannel    != null) { _productosChannel.Unsubscribe();    _productosChannel    = null; }
                if (_categoriasChannel  != null) { _categoriasChannel.Unsubscribe();  _categoriasChannel  = null; }
                if (_empleadosChannel   != null) { _empleadosChannel.Unsubscribe();   _empleadosChannel   = null; }
                if (_usuariosChannel    != null) { _usuariosChannel.Unsubscribe();    _usuariosChannel    = null; }
                if (_movimientosChannel != null) { _movimientosChannel.Unsubscribe(); _movimientosChannel = null; }
                if (_movProductosChannel!= null) { _movProductosChannel.Unsubscribe();_movProductosChannel= null; }

                _iniciado = false;
                System.Diagnostics.Debug.WriteLine("GestorRealtime detenido correctamente.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al detener GestorRealtime: {ex.Message}");
            }
        }
    }
}
