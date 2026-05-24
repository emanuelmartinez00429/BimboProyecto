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
        private static Client _client;

        public static async Task<Client> GetClientAsync()
        {
            if (_client == null)
            {
                if (_client == null)
                {
                    string url = Environment.GetEnvironmentVariable("SUPABASE_URL")
                        ?? ConfigurationManager.AppSettings["SUPABASE_URL"];
                    string key = Environment.GetEnvironmentVariable("SUPABASE_KEY")
                        ?? ConfigurationManager.AppSettings["SUPABASE_KEY"];

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
