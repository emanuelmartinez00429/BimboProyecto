using CapaAplicacion.Perfil;
using CapaAplicacion.Preferencias;
using CapaAplicacion.Preferencias.Dtos;
using CapaAplicacion.Preferencias.Interfaces;
using CapaDatos.Repositorios.Usuario;
using CapaDominio;

namespace CapaDatos.Perfil;

public class PerfilUsuarioService : IPerfilUsuarioService
{
    private readonly IPreferenciasUsuarioRepository _preferencias;

    // Registro del DI: addSingleton con este ctor; el repo de preferencias ya está registrado.
    public PerfilUsuarioService(IPreferenciasUsuarioRepository preferencias)
    {
        _preferencias = preferencias;
    }

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

            // Apodo personal (preferencia 'apodo', ámbito global): si el usuario definió
            // uno, gana sobre el nombre real para el nombre para mostrar (saludo y
            // tarjeta). Nunca reescribe nombreEmpleado/apellidoEmpleado.
            string? apodo = null;
            var prefs = await _preferencias.ObtenerTodasAsync(idUsuario);
            if (prefs.Success)
            {
                var fila = prefs.Value?.FirstOrDefault(p => p.Clave == ClavesPreferencia.Apodo);
                apodo = fila is null
                    ? null
                    : ValorPreferencia.Texto(fila.ValorJson, "").Trim() is { Length: > 0 } t ? t : null;
            }

            PerfilActual = new PerfilUsuario
            {
                NombreEmpleado = nombre,
                ApellidoEmpleado = apellido,
                NombreCompleto =
                    apodo is { Length: > 0 }
                        ? apodo
                        : nombreCompleto.Length > 0 ? nombreCompleto : usuario.aliasUsuario,
                Iniciales      = iniciales.Length > 0 ? iniciales : "??",
                NombreUsuario  = usuario.aliasUsuario,
                Correo         = usuario.empleados?.correoEmpleado ?? usuario.aliasUsuario,
                NombreRol      = usuario.nombre_Rol,
                Apodo          = apodo,
            };
        }
        catch { /* continúa con perfil null */ }
    }

    public void Limpiar() => PerfilActual = null;
}
