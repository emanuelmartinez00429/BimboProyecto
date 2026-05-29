using Supabase;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace ServicioConexión.Conexion
{
    /// <summary>
    /// Singleton thread-safe del cliente Supabase.
    /// Patrón double-check locking con SemaphoreSlim (asíncrono, no bloquea el UI thread).
    ///
    /// NOTA DE ARQUITECTURA: existe una copia de esta clase en
    /// ServicioConexión\Conexion\ConexionSupabase.cs (mismo namespace, otro ensamblado).
    /// CapaDatos compila ESTA copia (no referencia el proyecto ServicioConexión), así que
    /// es la que usan los repositorios y RealtimeService. Mantener ambas en sincronía o,
    /// idealmente, consolidar en una sola (ver pendiente de arquitectura).
    /// </summary>
    public class ConexionSupabase
    {
        public const int TimeoutSeconds = 10;

        private static Client? _client;
        private static readonly SemaphoreSlim _initLock = new(1, 1);

        public static async Task<Client> GetClientAsync()
        {
            if (_client is not null) return _client;   // fast-path sin lock

            await _initLock.WaitAsync();
            try
            {
                if (_client is not null) return _client;  // double-check real dentro del lock

                string? url = Environment.GetEnvironmentVariable("SUPABASE_URL")
                    ?? ConfigurationManager.AppSettings["SUPABASE_URL"];
                string? key = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                    ?? ConfigurationManager.AppSettings["SUPABASE_KEY"];

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException(
                        "SUPABASE_URL / SUPABASE_KEY no configurados (env var o App.config).");

                var options = new SupabaseOptions
                {
                    AutoConnectRealtime = true,
                    AutoRefreshToken    = true,
                };

                var client = new Client(url, key, options);
                await client.InitializeAsync();   // inicializa primero…
                _client = client;                 // …y solo entonces publica
                return _client;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// Libera por completo el cliente Supabase actual (socket Realtime, su WebsocketClient
        /// interno y todos los <c>System.Timers.Timer</c>/<c>System.Threading.Timer</c> asociados)
        /// y deja el singleton en null. El próximo <see cref="GetClientAsync"/> construye un cliente
        /// limpio.
        ///
        /// Se invoca al cerrar sesión: evita que canales, handlers y timers de Realtime se acumulen
        /// entre sesiones de login (la librería deduplica canales por topic y nunca los libera por
        /// su cuenta, por lo que reutilizar el mismo cliente arrastra todo lo acumulado).
        /// </summary>
        public static async Task ResetAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                var old = _client;
                _client = null;            // publica el null primero: nuevas llamadas reconstruyen
                if (old is null) return;

                try
                {
                    // Disconnect() cancela el heartbeat (Task loop) y detiene el socket.
                    old.Realtime.Disconnect();
                    // Disponer el socket libera el WebsocketClient y sus dos System.Threading.Timer
                    // (_lastChanceTimer, _errorReconnectTimer), que Disconnect() por sí solo no libera.
                    (old.Realtime.Socket as IDisposable)?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConexionSupabase] Error en ResetAsync: {ex.Message}");
                }
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}
