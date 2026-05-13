using CapaDatos.Modelados.Productos;
using CapaDatos.Modelados.Usuarios;
using ServicioConexión.Conexion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDatos.Repositorios.Usuario
{
    public class RepositorioEmpleado
    {
        public static async Task<List<Empleados>> obtenerEmpleados()
        {
            try
            {

                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client.From<Empleados>()
                                            .Get();

                return resultado?.Models ?? new List<Empleados>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener categorías: {ex.Message}");
                throw;
            }
        }
        public static async Task<List<Empleados>> actualizarEmpleados(Empleados empleado)
        {
            try 
            {
                var client = await ConexionSupabase.GetClientAsync();
                var resultado = await client.From<Empleados>()
                                            .Update(empleado);
                return resultado?.Models ?? new List<Empleados>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener categorías: {ex.Message}");
                throw;
            }
        }
    }
}
