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
                using var ctsObtener = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client.From<Empleados>()
                                            .Get(ctsObtener.Token);

                return resultado?.Models ?? new List<Empleados>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener empleados");
                throw;
            }
        }
        public static async Task<List<Empleados>> actualizarEmpleados(Empleados empleado)
        {
            try 
            {
                var client = await ConexionSupabase.GetClientAsync();
                using var ctsActualizar = new CancellationTokenSource(TimeSpan.FromSeconds(ConexionSupabase.TimeoutSeconds));
                var resultado = await client.From<Empleados>()
                                            .Update(empleado, null, ctsActualizar.Token);
                return resultado?.Models ?? new List<Empleados>();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error al obtener empleados");
                throw;
            }
        }
    }
}
