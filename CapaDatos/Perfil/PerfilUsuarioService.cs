using CapaAplicacion.Perfil;
using CapaDatos.Repositorios.Usuario;
using CapaDominio;

namespace CapaDatos.Perfil;

public class PerfilUsuarioService : IPerfilUsuarioService
{
    public PerfilUsuario? PerfilActual { get; private set; }

    public async Task CargarAsync(int idUsuario)
    {
        try
        {
            var usuario = await RepositorioUsuario.ObtenerPorIdAsync(idUsuario);
            if (usuario == null) return;

            string nombre         = usuario.empleados?.nombreEmpleado   ?? "";
            string apellido       = usuario.empleados?.apellidoEmpleado ?? "";
            string nombreCompleto = $"{nombre} {apellido}".Trim();

            string ini1     = nombre.Length   > 0 ? nombre[0].ToString()   : "";
            string ini2     = apellido.Length > 0 ? apellido[0].ToString() : "";
            string iniciales = $"{ini1}{ini2}".ToUpper();

            PerfilActual = new PerfilUsuario
            {
                NombreEmpleado = nombre,
                ApellidoEmpleado = apellido,
                NombreCompleto = nombreCompleto.Length > 0 ? nombreCompleto : usuario.aliasUsuario,
                Iniciales      = iniciales.Length > 0 ? iniciales : "??",
                NombreUsuario  = usuario.aliasUsuario,
                Correo         = usuario.empleados?.correoEmpleado ?? usuario.aliasUsuario,
                NombreRol      = usuario.nombre_Rol,
            };
        }
        catch { /* continúa con perfil null */ }
    }

    public void Limpiar() => PerfilActual = null;
}
