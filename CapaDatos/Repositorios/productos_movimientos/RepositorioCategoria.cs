using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapaDatos.Modelados.Productos;
using ServicioConexión.Conexion;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioCategoria
    {
        public static async Task<List<Categoria>> ObtenerCategorias()
        {
            try
            {

                var client = await ConexionSupabase.GetClientAsync();
                using var ctsObtener = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client.From<Categoria>()
                                            .Get(ctsObtener.Token);

                return resultado?.Models ?? new List<Categoria>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener categorías");
                throw;
            }
        }
        public static async Task<Categoria> InsertarCategoria(Categoria categoria)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsInsertar = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var response = await client.From<Categoria>()
                                           .Insert(categoria, cancellationToken: ctsInsertar.Token);
                return response.Model ?? categoria;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al insertar categoría");
                throw;
            }
        }
    }
}
