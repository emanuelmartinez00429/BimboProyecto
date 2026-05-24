using CapaDatos.Modelados.Productos;
using ServicioConexión.Conexion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDatos.Repositorios
{
    public class RepositorioPais
    {
        public async Task<List<Paises>> ObtenerTodosLosPaises()
        {
            var client = await ConexionSupabase.GetClientAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
            var resultado = await client
                .From<Paises>()
                .Get(cts.Token);

            return resultado.Models;
        }
    }
}
