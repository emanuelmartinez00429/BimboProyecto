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
    }
}
