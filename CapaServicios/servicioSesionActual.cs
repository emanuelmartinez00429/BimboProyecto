using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapaDominio
{
    public static class servicioSesionActual
    {
        // Mantenida por compatibilidad con código legado — no se asigna en el nuevo flujo de login
        public static Supabase.Gotrue.Session? Sesion { get; private set; }

        public static int    IdUsuario     { get; private set; }
        public static string NombreUsuario { get; private set; } = "";

        public static void Iniciar(int idUsuario, string email)
        {
            IdUsuario     = idUsuario;
            NombreUsuario = email;
            Sesion        = null;
        }

        public static void Cerrar()
        {
            Sesion        = null;
            IdUsuario     = 0;
            NombreUsuario = "";
        }
    }
}
