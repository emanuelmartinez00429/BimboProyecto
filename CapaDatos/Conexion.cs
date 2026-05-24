using Supabase;
using Supabase.Interfaces;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ServicioConexión.Conexion
{
    public class ConexionSupabase
    {
        public const int TimeoutSeconds = 10;

        private static Client _client;

        public static async Task<Client> GetClientAsync()
        {
            if (_client == null)
            {
                if (_client == null)
                {
                    string url = ConfigurationManager.AppSettings["SUPABASE_URL"];
                    string key = ConfigurationManager.AppSettings["SUPABASE_KEY"];

                    var options = new SupabaseOptions
                    {
                        AutoConnectRealtime = true,
                        AutoRefreshToken = true,
                        
                    };

                    _client = new Client(url, key, options);
                    await _client.InitializeAsync();
                }
            }
            return _client;
        }
    }
}

