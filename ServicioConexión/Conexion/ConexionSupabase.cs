using Supabase;
using System.Configuration;

namespace ServicioConexión.Conexion
{
    /// <summary>
    /// Singleton thread-safe del cliente Supabase.
    /// Patrón double-check locking con SemaphoreSlim (asíncrono, no bloquea UI thread).
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
        /// Apaga el cliente Supabase actual —el timer de auto-refresh de Gotrue, el socket Realtime
        /// y el WebsocketClient interno con sus <c>System.Threading.Timer</c>— y deja el singleton
        /// en null. El próximo <see cref="GetClientAsync"/> construye un cliente limpio.
        ///
        /// Se invoca al cerrar sesión: evita que canales, handlers y timers se acumulen entre
        /// sesiones de login (la librería deduplica canales por topic y nunca los libera por su
        /// cuenta, por lo que reutilizar el mismo cliente arrastra todo lo acumulado).
        ///
        /// Ojo con el nombre: <c>Supabase.Client</c> <b>no</b> implementa <c>IDisposable</c> — no hay
        /// un "liberar todo". Lo que se puede apagar es exactamente lo de arriba; el resto queda
        /// para el GC cuando se suelta la última referencia, que es lo que hace el <c>_client = null</c>.
        /// </summary>
        public static async Task ResetAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                var old = _client;
                _client = null;            // publica el null primero: nuevas llamadas reconstruyen
                if (old is null) return;

                // Apagado local del auto-refresh de Gotrue. SignOut() también lo apaga (emite
                // AuthState.SignedOut y TokenRefresh detiene el timer), pero SignOut() es una
                // llamada de RED envuelta en try/catch: si el logout ocurre sin conexión o el
                // endpoint no responde, el timer sobrevive a la sesión y se queda pidiendo
                // tokens de una sesión que ya no existe. Shutdown() no toca la red, así que es
                // el único apagado que no depende de que algo remoto funcione.
                // Va en su propio try para que un fallo acá no impida disponer el socket.
                try
                {
                    old.Auth.Shutdown();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConexionSupabase] Auth.Shutdown() falló: {ex.Message}");
                }

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
