using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDominio
{
    public static class servicioSesionActual
    {
        public static Supabase.Gotrue.Session? Sesion { get; private set; }
        public static int IdUsuario { get; set; }
        public static string NombreUsuario { get; set; } = "";

        public static void Iniciar(Supabase.Gotrue.Session sesion, int idUsuario, string nombre)
        {
            Sesion = sesion;
            IdUsuario = idUsuario;
            NombreUsuario = nombre;
        }

        public static void Cerrar()
        {
            Sesion = null;
            IdUsuario = 0;
            NombreUsuario = "";
        }
    }
}
