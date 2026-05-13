using CapaDatos.Modelados;
using CapaDatos.Modelados.Productos;
using ServicioConexión.Conexion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDatos.Repositorios.productos_movimientos
{
    public class RepositorioProducto
    {
        public static async Task<List<Productos>> ObtenerTodosLosProductos()
        {
            try 
            { 
                    var client = await ConexionSupabase.GetClientAsync();
                    var resultado = await client
                                                .From<Productos>()
                                                .Get();
                return resultado?.Models ?? new List<Productos>();
            }
            catch (Exception ex)
            {

                Console.WriteLine($"Error al obtener productos: {ex.Message}");
                throw;
            }
        }

        public static async Task<List<Productos>> obtenerProductosJoin()
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client
                                            .From<Productos>()
                                            .Select("*, presentacion_producto(*), fabricante(*), categoria(*), paises(*)")
                                            .Order("id_producto", Supabase.Postgrest.Constants.Ordering.Ascending)
                                            .Get();
                return resultado?.Models ?? new List<Productos>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener productos: {ex.Message}");
                throw;
            }
        }

        public static async Task<ProductosInsertar> ingresarProducto(ProductosInsertar datos)
        {
            try
            {
                var client = await ConexionSupabase.GetClientAsync();
                var response = await client .From<ProductosInsertar>()
                                            .Insert(datos);
                return response.Model ?? null;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al ingresar producto: {ex.Message}");
                throw;
            }
        }

    }
}
