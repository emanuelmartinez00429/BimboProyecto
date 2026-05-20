using CapaDatos.Repositorios.Usuario;

namespace CapaDominio
{
    public class PerfilUsuario
    {
        public string NombreCompleto { get; set; } = "";
        public string Iniciales     { get; set; } = "";
        public string NombreUsuario { get; set; } = "";
        public string Correo        { get; set; } = "";
        public string NombreRol     { get; set; } = "";
    }

    public static class ServicioPerfilUsuario
    {
        public static PerfilUsuario? PerfilActual { get; private set; }

        public static async Task CargarAsync()
        {
            try
            {
                int idActual = servicioSesionActual.IdUsuario;
                var lista    = await RepositorioUsuario.obtenerUsuarios();
                var usuario  = lista.FirstOrDefault(u => u.idUsuario == idActual);
                if (usuario == null) return;

                string nombre   = usuario.empleados?.nombreEmpleado   ?? "";
                string apellido = usuario.empleados?.apellidoEmpleado ?? "";
                string nombreCompleto = $"{nombre} {apellido}".Trim();

                string ini1 = nombre.Length   > 0 ? nombre[0].ToString()   : "";
                string ini2 = apellido.Length > 0 ? apellido[0].ToString() : "";
                string iniciales = $"{ini1}{ini2}".ToUpper();

                PerfilActual = new PerfilUsuario
                {
                    NombreCompleto = nombreCompleto.Length > 0
                                        ? nombreCompleto
                                        : usuario.correoUsuario,
                    Iniciales      = iniciales.Length > 0 ? iniciales : "??",
                    NombreUsuario  = usuario.correoUsuario,
                    Correo         = usuario.empleados?.correoEmpleado ?? usuario.correoUsuario,
                    NombreRol      = usuario.nombre_Rol,
                };
            }
            catch { /* continúa con perfil null */ }
        }

        public static void Limpiar() => PerfilActual = null;
    }
}
